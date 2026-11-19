using PropertyManagement.Domain.Common;
using PropertyManagement.Domain.Entities.Payments.Events;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Domain.Entities.Payments;

public sealed class PaymentReconciliationRecord : IOrganizationScoped, IHasDomainEvents
{
    private readonly List<IDomainEvent> _domainEvents = [];

    private PaymentReconciliationRecord() { }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid BatchId { get; private set; }
    public PaymentProviderType Provider { get; private set; }
    public Guid? InternalPaymentId { get; private set; }
    public string? ProviderTransactionId { get; private set; }
    public string Currency { get; private set; } = CurrencyCode.Default;
    public decimal? ExpectedAmount { get; private set; }
    public decimal? ProviderAmount { get; private set; }
    public decimal? ExpectedFee { get; private set; }
    public decimal? ProviderFee { get; private set; }
    public decimal? ExpectedNetAmount { get; private set; }
    public decimal? ProviderNetAmount { get; private set; }
    public ReconciliationStatus Status { get; private set; }
    public string? MismatchDetails { get; private set; }
    public string? ResolutionNotes { get; private set; }
    public Guid? ResolvedByUserId { get; private set; }
    public DateTime? ResolvedAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();
    public void ClearDomainEvents() => _domainEvents.Clear();

    public static PaymentReconciliationRecord Create(
        Guid organizationId,
        Guid batchId,
        PaymentProviderType provider,
        string currency,
        ReconciliationStatus status,
        DateTime utcNow,
        Guid? internalPaymentId = null,
        string? providerTransactionId = null,
        decimal? expectedAmount = null,
        decimal? providerAmount = null,
        decimal? expectedFee = null,
        decimal? providerFee = null,
        decimal? expectedNetAmount = null,
        decimal? providerNetAmount = null,
        string? mismatchDetails = null)
    {
        if (organizationId == Guid.Empty) throw new ArgumentException("Organization ID cannot be empty.", nameof(organizationId));
        if (batchId == Guid.Empty) throw new ArgumentException("Batch ID cannot be empty.", nameof(batchId));

        var record = new PaymentReconciliationRecord
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            BatchId = batchId,
            Provider = provider,
            InternalPaymentId = internalPaymentId,
            ProviderTransactionId = string.IsNullOrWhiteSpace(providerTransactionId) ? null : providerTransactionId.Trim(),
            Currency = CurrencyCode.Normalize(currency),
            ExpectedAmount = expectedAmount.HasValue ? MoneyRounding.Round(expectedAmount.Value) : null,
            ProviderAmount = providerAmount.HasValue ? MoneyRounding.Round(providerAmount.Value) : null,
            ExpectedFee = expectedFee.HasValue ? MoneyRounding.Round(expectedFee.Value) : null,
            ProviderFee = providerFee.HasValue ? MoneyRounding.Round(providerFee.Value) : null,
            ExpectedNetAmount = expectedNetAmount.HasValue ? MoneyRounding.Round(expectedNetAmount.Value) : null,
            ProviderNetAmount = providerNetAmount.HasValue ? MoneyRounding.Round(providerNetAmount.Value) : null,
            Status = status,
            MismatchDetails = string.IsNullOrWhiteSpace(mismatchDetails) ? null : mismatchDetails.Trim(),
            CreatedAtUtc = utcNow
        };

        if (status != ReconciliationStatus.Matched)
        {
            record._domainEvents.Add(new PaymentReconciliationExceptionDetectedEvent(
                record.Id,
                record.OrganizationId,
                record.Provider,
                record.Status,
                record.ProviderTransactionId,
                record.InternalPaymentId));
        }

        return record;
    }

    public void Resolve(Guid resolvedByUserId, string resolutionNotes, DateTime utcNow)
    {
        if (resolvedByUserId == Guid.Empty) throw new ArgumentException("Resolver user ID cannot be empty.", nameof(resolvedByUserId));
        ArgumentException.ThrowIfNullOrWhiteSpace(resolutionNotes, nameof(resolutionNotes));

        if (Status == ReconciliationStatus.Matched)
        {
            throw new InvalidOperationException("Matched reconciliation records do not require resolution.");
        }

        if (Status == ReconciliationStatus.Resolved)
        {
            throw new InvalidOperationException("Reconciliation record is already resolved.");
        }

        Status = ReconciliationStatus.Resolved;
        ResolvedByUserId = resolvedByUserId;
        ResolutionNotes = resolutionNotes.Trim();
        ResolvedAtUtc = utcNow;
    }
}
