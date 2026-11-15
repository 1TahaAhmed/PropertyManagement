using MediatR;
using PropertyManagement.Application.Abstractions.Billing;
using PropertyManagement.Application.Abstractions.Repositories;
using PropertyManagement.Application.Common.Enums;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Application.Features.Billing.Responses;

namespace PropertyManagement.Application.Features.Billing.Charges;

public sealed class GetLeasePendingChargesQueryHandler(
    ILeaseRepository leaseRepository,
    IChargeRepository chargeRepository) : IRequestHandler<GetLeasePendingChargesQuery, Result<IReadOnlyList<ChargeResponse>>>
{
    public async Task<Result<IReadOnlyList<ChargeResponse>>> Handle(
        GetLeasePendingChargesQuery request,
        CancellationToken cancellationToken)
    {
        var lease = await leaseRepository.GetByIdAsync(request.LeaseId, request.OrganizationId, cancellationToken);
        if (lease is null || lease.OrganizationId != request.OrganizationId)
        {
            return Result<IReadOnlyList<ChargeResponse>>.Failure(new Error(
                Code: "Lease.NotFound",
                Message: "The specified lease was not found.",
                Type: ErrorType.NotFound));
        }

        var charges = await chargeRepository.GetPendingByLeaseAsync(
            request.LeaseId,
            request.OrganizationId,
            request.UpToPeriodEnd,
            cancellationToken);

        return Result<IReadOnlyList<ChargeResponse>>.Success(
            charges.Select(ChargeResponse.FromEntity).ToList());
    }
}
