using PropertyManagement.Domain.Common;
using PropertyManagement.Domain.Entities.Ledger.Events;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Domain.Entities.Ledger;

public sealed class JournalEntry : IOrganizationScoped, IHasDomainEvents
{
    private readonly List<JournalLine> _lines = [];
    private readonly List<IDomainEvent> _domainEvents = [];

    private JournalEntry() { }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public string EntryNumber { get; private set; } = string.Empty;
    public string SourceType { get; private set; } = string.Empty;
    public Guid SourceId { get; private set; }
    public string IdempotencyKey { get; private set; } = string.Empty;
    public string Currency { get; private set; } = CurrencyCode.Default;
    public string Description { get; private set; } = string.Empty;
    public LedgerEntryStatus Status { get; private set; }
    public decimal TotalDebits { get; private set; }
    public decimal TotalCredits { get; private set; }
    public DateTime EntryDateUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? PostedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public IReadOnlyCollection<JournalLine> Lines => _lines.AsReadOnly();
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();
    public void ClearDomainEvents() => _domainEvents.Clear();

    public static JournalEntry CreateDraft(
        Guid organizationId,
        string entryNumber,
        string sourceType,
        Guid sourceId,
        string idempotencyKey,
        string currency,
        string description,
        DateTime entryDateUtc,
        DateTime utcNow)
    {
        if (organizationId == Guid.Empty) throw new ArgumentException("Organization ID cannot be empty.", nameof(organizationId));
        if (sourceId == Guid.Empty) throw new ArgumentException("Source ID cannot be empty.", nameof(sourceId));
        ArgumentException.ThrowIfNullOrWhiteSpace(entryNumber, nameof(entryNumber));
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceType, nameof(sourceType));
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey, nameof(idempotencyKey));
        ArgumentException.ThrowIfNullOrWhiteSpace(description, nameof(description));

        return new JournalEntry
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            EntryNumber = entryNumber.Trim(),
            SourceType = sourceType.Trim(),
            SourceId = sourceId,
            IdempotencyKey = idempotencyKey.Trim(),
            Currency = CurrencyCode.Normalize(currency),
            Description = description.Trim(),
            Status = LedgerEntryStatus.Draft,
            TotalDebits = 0m,
            TotalCredits = 0m,
            EntryDateUtc = entryDateUtc,
            CreatedAtUtc = utcNow
        };
    }

    public JournalLine AddDebit(LedgerAccountCode accountCode, decimal amount, string description)
    {
        EnsureDraft();
        var line = JournalLine.Create(OrganizationId, Id, accountCode, amount, 0m, Currency, description);
        _lines.Add(line);
        RecalculateTotals();
        return line;
    }

    public JournalLine AddCredit(LedgerAccountCode accountCode, decimal amount, string description)
    {
        EnsureDraft();
        var line = JournalLine.Create(OrganizationId, Id, accountCode, 0m, amount, Currency, description);
        _lines.Add(line);
        RecalculateTotals();
        return line;
    }

    public void Post(DateTime utcNow)
    {
        EnsureDraft();

        if (_lines.Count < 2)
        {
            throw new InvalidOperationException("A posted journal entry must contain at least two lines.");
        }

        RecalculateTotals();

        if (TotalDebits <= 0m || TotalDebits != TotalCredits)
        {
            throw new InvalidOperationException(
                $"Journal entry is unbalanced: TotalDebits ({TotalDebits:0.00}) != TotalCredits ({TotalCredits:0.00}).");
        }

        Status = LedgerEntryStatus.Posted;
        PostedAtUtc = utcNow;

        _domainEvents.Add(new JournalEntryPostedEvent(
            Id,
            OrganizationId,
            EntryNumber,
            SourceType,
            SourceId,
            TotalDebits,
            Currency));
    }

    private void EnsureDraft()
    {
        if (Status == LedgerEntryStatus.Posted)
        {
            throw new InvalidOperationException("Posted journal entries are immutable and cannot be modified. Create a new reversing or adjustment entry instead.");
        }
    }

    private void RecalculateTotals()
    {
        TotalDebits = MoneyRounding.Round(_lines.Sum(l => l.Debit));
        TotalCredits = MoneyRounding.Round(_lines.Sum(l => l.Credit));
    }
}
