using PropertyManagement.Domain.Common;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Domain.Entities.Payments.Events;

public sealed record PaymentIntentCreatedEvent(
    Guid PaymentIntentId,
    Guid OrganizationId,
    Guid ResidentId,
    decimal Amount,
    string Currency,
    PaymentProviderType Provider,
    PaymentMethod PaymentMethod) : IDomainEvent;

public sealed record PaymentCreatedEvent(
    Guid PaymentId,
    Guid OrganizationId,
    Guid ResidentId,
    decimal Amount,
    string Currency,
    PaymentMethod PaymentMethod,
    PaymentProviderType Provider) : IDomainEvent;

public sealed record PaymentSucceededEvent(
    Guid PaymentId,
    Guid OrganizationId,
    Guid ResidentId,
    decimal Amount,
    string Currency,
    PaymentProviderType Provider,
    string? ProviderTransactionId) : IDomainEvent;

public sealed record PaymentFailedEvent(
    Guid PaymentId,
    Guid OrganizationId,
    Guid ResidentId,
    string Reason) : IDomainEvent;

public sealed record PaymentCancelledEvent(
    Guid PaymentId,
    Guid OrganizationId,
    Guid ResidentId,
    string Reason) : IDomainEvent;

public sealed record PaymentAllocatedEvent(
    Guid PaymentId,
    Guid AllocationId,
    Guid OrganizationId,
    Guid InvoiceId,
    decimal Amount,
    string Currency) : IDomainEvent;

public sealed record PaymentAllocationReversedEvent(
    Guid PaymentId,
    Guid AllocationId,
    Guid OrganizationId,
    Guid InvoiceId,
    decimal Amount,
    string Reason) : IDomainEvent;

public sealed record PaymentUnappliedCreditRecordedEvent(
    Guid PaymentId,
    Guid OrganizationId,
    Guid ResidentId,
    decimal UnappliedAmount,
    string Currency) : IDomainEvent;

public sealed record PaymentRefundedEvent(
    Guid PaymentId,
    Guid RefundId,
    Guid OrganizationId,
    Guid ResidentId,
    decimal RefundAmount,
    decimal TotalRefundedAmount,
    string Currency,
    bool IsFullRefund) : IDomainEvent;

public sealed record PaymentSettledEvent(
    Guid PaymentId,
    Guid OrganizationId,
    decimal GrossAmount,
    decimal ProviderFee,
    decimal NetSettlementAmount,
    string Currency) : IDomainEvent;

public sealed record PaymentReconciliationExceptionDetectedEvent(
    Guid ReconciliationRecordId,
    Guid OrganizationId,
    PaymentProviderType Provider,
    ReconciliationStatus Status,
    string? ProviderTransactionId,
    Guid? InternalPaymentId) : IDomainEvent;
