using MediatR;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Application.Features.Billing.Responses;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Application.Features.Billing.Charges;

public sealed record CreateChargeCommand(
    Guid OrganizationId,
    Guid LeaseId,
    ChargeType Type,
    string Description,
    decimal Quantity,
    decimal UnitPrice,
    DateOnly? PeriodStart = null,
    DateOnly? PeriodEnd = null) : IRequest<Result<ChargeResponse>>;
