namespace PropertyManagement.Domain.Enums;

public enum PaymentMethod
{
    Cash = 1,
    BankTransfer = 2,
    Card = 3,
    Ach = 4,
    DirectDebit = 5,
    Other = 6
}

public enum PaymentProviderType
{
    Manual = 1,
    Stripe = 2,
    Paymob = 3,
    Adyen = 4,
    PayPal = 5,
    Bank = 6,
    Simulated = 7
}

public enum PaymentStatus
{
    Pending = 1,
    Processing = 2,
    Succeeded = 3,
    Failed = 4,
    Cancelled = 5,
    PartiallyRefunded = 6,
    Refunded = 7
}

public enum PaymentIntentStatus
{
    Pending = 1,
    RequiresAction = 2,
    Processing = 3,
    Succeeded = 4,
    Failed = 5,
    Cancelled = 6,
    Expired = 7
}

public enum RefundStatus
{
    Pending = 1,
    Processing = 2,
    Succeeded = 3,
    Failed = 4
}

public enum OverpaymentPolicy
{
    KeepUnappliedCredit = 1,
    AutoApplyToOldestInvoices = 2,
    RejectOverpayment = 3
}

public enum AllocationStrategy
{
    Explicit = 1,
    OldestOverdueFirst = 2,
    OldestDueFirst = 3
}

public enum WebhookEventStatus
{
    Received = 1,
    Processed = 2,
    IgnoredDuplicate = 3,
    Failed = 4
}

public enum ReconciliationStatus
{
    Matched = 1,
    MissingInternalPayment = 2,
    MissingProviderTransaction = 3,
    AmountMismatch = 4,
    FeeMismatch = 5,
    SettlementMismatch = 6,
    DuplicateProviderTransaction = 7,
    Resolved = 8
}
