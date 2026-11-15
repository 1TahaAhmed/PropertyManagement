using MediatR;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Application.Features.Billing.Responses;

namespace PropertyManagement.Application.Features.Billing.Charges;

public sealed record VoidChargeCommand(
    Guid OrganizationId,
    Guid ChargeId,
    string Reason) : IRequest<Result<ChargeResponse>>;
