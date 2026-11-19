using PropertyManagement.Domain.Entities.Audit;
using PropertyManagement.Domain.Entities.Ledger;
using PropertyManagement.Domain.Entities.Payments;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Application.Features.Payments.Responses;

public sealed record PaymentIntentResponse(
    Guid Id,
    Guid OrganizationId,
    Guid ResidentId,
    Guid? LeaseId,
    Guid? TargetInvoiceId,
    decimal Amount,
    string Currency,
    PaymentMethod PaymentMethod,
    PaymentProviderType Provider,
    PaymentIntentStatus Status,
    string? ProviderIntentId,
    string? ClientSecret,
    string? ActionUrl,
    string? FailureReason,
    DateTime ExpiresAtUtc,
    DateTime CreatedAtUtc,
    string RowVersion)
{
    public static PaymentIntentResponse FromEntity(PaymentIntent intent) => new(
        intent.Id,
        intent.OrganizationId,
        intent.ResidentId,
        intent.LeaseId,
        intent.TargetInvoiceId,
        intent.Amount,
        intent.Currency,
        intent.PaymentMethod,
        intent.Provider,
        intent.Status,
        intent.ProviderIntentId,
        intent.ClientSecret,
        intent.ActionUrl,
        intent.FailureReason,
        intent.ExpiresAtUtc,
        intent.CreatedAtUtc,
        Convert.ToBase64String(intent.RowVersion));
}

public sealed record PaymentAllocationResponse(
    Guid Id,
    Guid PaymentId,
    Guid InvoiceId,
    decimal Amount,
    string Currency,
    DateTime AllocatedAtUtc,
    Guid? AllocatedByUserId,
    bool IsReversed,
    DateTime? ReversedAtUtc,
    string? ReversalReason,
    Guid? ReversedByUserId)
{
    public static PaymentAllocationResponse FromEntity(PaymentAllocation allocation) => new(
        allocation.Id,
        allocation.PaymentId,
        allocation.InvoiceId,
        allocation.Amount,
        allocation.Currency,
        allocation.AllocatedAtUtc,
        allocation.AllocatedByUserId,
        allocation.IsReversed,
        allocation.ReversedAtUtc,
        allocation.ReversalReason,
        allocation.ReversedByUserId);
}

public sealed record RefundResponse(
    Guid Id,
    Guid PaymentId,
    decimal Amount,
    string Currency,
    RefundStatus Status,
    string? ProviderRefundId,
    string Reason,
    string? FailureReason,
    Guid? RequestedByUserId,
    DateTime CreatedAtUtc,
    DateTime? CompletedAtUtc,
    string RowVersion)
{
    public static RefundResponse FromEntity(Refund refund) => new(
        refund.Id,
        refund.PaymentId,
        refund.Amount,
        refund.Currency,
        refund.Status,
        refund.ProviderRefundId,
        refund.Reason,
        refund.FailureReason,
        refund.RequestedByUserId,
        refund.CreatedAtUtc,
        refund.CompletedAtUtc,
        Convert.ToBase64String(refund.RowVersion));
}

public sealed record PaymentResponse(
    Guid Id,
    Guid OrganizationId,
    Guid ResidentId,
    Guid? LeaseId,
    Guid? PaymentIntentId,
    string? PaymentNumber,
    decimal Amount,
    string Currency,
    decimal ProviderFee,
    decimal NetSettlementAmount,
    decimal AllocatedAmount,
    decimal UnappliedAmount,
    decimal RefundedAmount,
    decimal RemainingRefundableAmount,
    PaymentMethod PaymentMethod,
    PaymentProviderType Provider,
    PaymentStatus Status,
    OverpaymentPolicy OverpaymentPolicy,
    string? ProviderPaymentId,
    string? ProviderCustomerId,
    string? ProviderTransactionId,
    string? ProviderStatus,
    string? Reference,
    string? FailureReason,
    DateTime? ReceivedAtUtc,
    DateTime? ConfirmedAtUtc,
    DateTime? SettledAtUtc,
    DateTime CreatedAtUtc,
    IReadOnlyList<PaymentAllocationResponse> Allocations,
    IReadOnlyList<RefundResponse> Refunds,
    string RowVersion)
{
    public static PaymentResponse FromEntity(Payment payment) => new(
        payment.Id,
        payment.OrganizationId,
        payment.ResidentId,
        payment.LeaseId,
        payment.PaymentIntentId,
        payment.PaymentNumber,
        payment.Amount,
        payment.Currency,
        payment.ProviderFee,
        payment.NetSettlementAmount,
        payment.AllocatedAmount,
        payment.UnappliedAmount,
        payment.RefundedAmount,
        payment.RemainingRefundableAmount,
        payment.PaymentMethod,
        payment.Provider,
        payment.Status,
        payment.OverpaymentPolicy,
        payment.ProviderPaymentId,
        payment.ProviderCustomerId,
        payment.ProviderTransactionId,
        payment.ProviderStatus,
        payment.Reference,
        payment.FailureReason,
        payment.ReceivedAtUtc,
        payment.ConfirmedAtUtc,
        payment.SettledAtUtc,
        payment.CreatedAtUtc,
        payment.Allocations.Select(PaymentAllocationResponse.FromEntity).ToList(),
        payment.Refunds.Select(RefundResponse.FromEntity).ToList(),
        Convert.ToBase64String(payment.RowVersion));
}

public sealed record WebhookProcessingResponse(
    string EventId,
    PaymentProviderType Provider,
    WebhookEventStatus Status,
    bool IsDuplicate,
    Guid? PaymentId,
    string? Message);

public sealed record ReconciliationRecordResponse(
    Guid Id,
    Guid BatchId,
    PaymentProviderType Provider,
    Guid? InternalPaymentId,
    string? ProviderTransactionId,
    string Currency,
    decimal? ExpectedAmount,
    decimal? ProviderAmount,
    decimal? ExpectedFee,
    decimal? ProviderFee,
    decimal? ExpectedNetAmount,
    decimal? ProviderNetAmount,
    ReconciliationStatus Status,
    string? MismatchDetails,
    string? ResolutionNotes,
    Guid? ResolvedByUserId,
    DateTime? ResolvedAtUtc,
    DateTime CreatedAtUtc)
{
    public static ReconciliationRecordResponse FromEntity(PaymentReconciliationRecord record) => new(
        record.Id,
        record.BatchId,
        record.Provider,
        record.InternalPaymentId,
        record.ProviderTransactionId,
        record.Currency,
        record.ExpectedAmount,
        record.ProviderAmount,
        record.ExpectedFee,
        record.ProviderFee,
        record.ExpectedNetAmount,
        record.ProviderNetAmount,
        record.Status,
        record.MismatchDetails,
        record.ResolutionNotes,
        record.ResolvedByUserId,
        record.ResolvedAtUtc,
        record.CreatedAtUtc);
}

public sealed record ReconciliationBatchResponse(
    Guid BatchId,
    PaymentProviderType Provider,
    int TotalChecked,
    int MatchedCount,
    int ExceptionCount,
    IReadOnlyList<ReconciliationRecordResponse> Records);

public sealed record JournalLineResponse(
    Guid Id,
    LedgerAccountCode AccountCode,
    LedgerAccountType AccountType,
    decimal Debit,
    decimal Credit,
    string Currency,
    string Description)
{
    public static JournalLineResponse FromEntity(JournalLine line) => new(
        line.Id,
        line.AccountCode,
        line.AccountType,
        line.Debit,
        line.Credit,
        line.Currency,
        line.Description);
}

public sealed record JournalEntryResponse(
    Guid Id,
    Guid OrganizationId,
    string EntryNumber,
    string SourceType,
    Guid SourceId,
    string Currency,
    string Description,
    LedgerEntryStatus Status,
    decimal TotalDebits,
    decimal TotalCredits,
    DateTime EntryDateUtc,
    DateTime? PostedAtUtc,
    IReadOnlyList<JournalLineResponse> Lines)
{
    public static JournalEntryResponse FromEntity(JournalEntry entry) => new(
        entry.Id,
        entry.OrganizationId,
        entry.EntryNumber,
        entry.SourceType,
        entry.SourceId,
        entry.Currency,
        entry.Description,
        entry.Status,
        entry.TotalDebits,
        entry.TotalCredits,
        entry.EntryDateUtc,
        entry.PostedAtUtc,
        entry.Lines.Select(JournalLineResponse.FromEntity).ToList());
}

public sealed record FinancialAuditLogResponse(
    Guid Id,
    Guid OrganizationId,
    Guid? ActorUserId,
    string Action,
    string EntityType,
    Guid EntityId,
    string DetailsJson,
    string? CorrelationId,
    DateTime OccurredAtUtc)
{
    public static FinancialAuditLogResponse FromEntity(FinancialAuditLog entry) => new(
        entry.Id,
        entry.OrganizationId,
        entry.ActorUserId,
        entry.Action,
        entry.EntityType,
        entry.EntityId,
        entry.DetailsJson,
        entry.CorrelationId,
        entry.OccurredAtUtc);
}
