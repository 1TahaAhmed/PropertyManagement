using MediatR;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Application.Features.Billing.Responses;

namespace PropertyManagement.Application.Features.Billing.Charges;

public sealed record GetLeasePendingChargesQuery(
    Guid OrganizationId,
    Guid LeaseId,
    DateOnly? UpToPeriodEnd = null) : IRequest<Result<IReadOnlyList<ChargeResponse>>>;
