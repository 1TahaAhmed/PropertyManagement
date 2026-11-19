using PropertyManagement.Domain.Common;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Domain.Entities.Ledger;

public sealed class JournalLine : IOrganizationScoped
{
    private JournalLine() { }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid JournalEntryId { get; private set; }
    public LedgerAccountCode AccountCode { get; private set; }
    public LedgerAccountType AccountType { get; private set; }
    public decimal Debit { get; private set; }
    public decimal Credit { get; private set; }
    public string Currency { get; private set; } = CurrencyCode.Default;
    public string Description { get; private set; } = string.Empty;

    internal static JournalLine Create(
        Guid organizationId,
        Guid journalEntryId,
        LedgerAccountCode accountCode,
        decimal debit,
        decimal credit,
        string currency,
        string description)
    {
        if (organizationId == Guid.Empty) throw new ArgumentException("Organization ID cannot be empty.", nameof(organizationId));
        if (journalEntryId == Guid.Empty) throw new ArgumentException("JournalEntry ID cannot be empty.", nameof(journalEntryId));
        ArgumentException.ThrowIfNullOrWhiteSpace(description, nameof(description));

        var roundedDebit = MoneyRounding.Round(debit);
        var roundedCredit = MoneyRounding.Round(credit);

        if (roundedDebit < 0m || roundedCredit < 0m)
        {
            throw new ArgumentException("Debit and Credit amounts cannot be negative.");
        }

        if ((roundedDebit > 0m && roundedCredit > 0m) || (roundedDebit == 0m && roundedCredit == 0m))
        {
            throw new ArgumentException("Each journal line must have either a positive Debit or a positive Credit, not both or neither.");
        }

        return new JournalLine
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            JournalEntryId = journalEntryId,
            AccountCode = accountCode,
            AccountType = ResolveAccountType(accountCode),
            Debit = roundedDebit,
            Credit = roundedCredit,
            Currency = CurrencyCode.Normalize(currency),
            Description = description.Trim()
        };
    }

    private static LedgerAccountType ResolveAccountType(LedgerAccountCode code) => code switch
    {
        LedgerAccountCode.Cash => LedgerAccountType.Asset,
        LedgerAccountCode.PaymentProviderClearing => LedgerAccountType.Asset,
        LedgerAccountCode.AccountsReceivable => LedgerAccountType.Asset,
        LedgerAccountCode.CustomerUnappliedCredit => LedgerAccountType.Liability,
        LedgerAccountCode.RentalRevenue => LedgerAccountType.Revenue,
        LedgerAccountCode.ProcessingFeeExpense => LedgerAccountType.Expense,
        _ => throw new ArgumentOutOfRangeException(nameof(code), code, "Unknown ledger account code.")
    };
}
