using MediatR;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Application.Features.Billing.Responses;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Application.Features.Billing.Invoices;

public sealed record ApplyInvoiceAdjustmentCommand(
    Guid OrganizationId,
    Guid InvoiceId,
    InvoiceAdjustmentType Type,
    ChargeType ChargeType,
    decimal Amount,
    string Description,
    string Reason,
    Guid ActorUserId,
    string? RowVersion = null) : IRequest<Result<InvoiceResponse>>;
