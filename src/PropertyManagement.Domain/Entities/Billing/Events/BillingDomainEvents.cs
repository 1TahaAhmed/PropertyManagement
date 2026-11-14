using PropertyManagement.Domain.Common;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Domain.Entities.Billing.Events;

public sealed record InvoiceCreatedEvent(
    Guid InvoiceId,
    Guid OrganizationId,
    Guid LeaseId,
    Guid ResidentId) : IDomainEvent;

public sealed record InvoiceIssuedEvent(
    Guid InvoiceId,
    Guid OrganizationId,
    Guid LeaseId,
    Guid ResidentId,
    string InvoiceNumber,
    decimal Total,
    string Currency,
    DateOnly DueDate) : IDomainEvent;

public sealed record InvoiceVoidedEvent(
    Guid InvoiceId,
    Guid OrganizationId,
    Guid LeaseId,
    string? InvoiceNumber,
    string Reason) : IDomainEvent;

public sealed record InvoiceAdjustedEvent(
    Guid InvoiceId,
    Guid AdjustmentId,
    Guid OrganizationId,
    InvoiceAdjustmentType AdjustmentType,
    ChargeType ChargeType,
    decimal Amount,
    decimal NewTotal,
    decimal NewBalanceDue) : IDomainEvent;

public sealed record InvoiceMarkedOverdueEvent(
    Guid InvoiceId,
    Guid OrganizationId,
    DateOnly DueDate) : IDomainEvent;
