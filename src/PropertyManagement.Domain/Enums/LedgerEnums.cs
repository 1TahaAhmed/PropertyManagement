namespace PropertyManagement.Domain.Enums;

public enum LedgerAccountType
{
    Asset = 1,
    Liability = 2,
    Equity = 3,
    Revenue = 4,
    Expense = 5
}

public enum LedgerAccountCode
{
    Cash = 1000,
    PaymentProviderClearing = 1050,
    AccountsReceivable = 1200,
    CustomerUnappliedCredit = 2100,
    RentalRevenue = 4000,
    LateFeeRevenue = 4100,
    OtherRevenue = 4200,
    ProcessingFeeExpense = 5100
}

public enum LedgerEntryStatus
{
    Draft = 1,
    Posted = 2
}
