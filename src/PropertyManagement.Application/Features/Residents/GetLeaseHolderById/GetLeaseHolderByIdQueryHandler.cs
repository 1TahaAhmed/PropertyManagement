using MediatR;
using PropertyManagement.Application.Abstractions.Residents;
using PropertyManagement.Application.Common.Enums;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Application.Features.Residents.Responses;

namespace PropertyManagement.Application.Features.Residents.GetLeaseHolderById;

public sealed class GetLeaseHolderByIdQueryHandler(
    IResidentRepository residentRepository)
    : IRequestHandler<GetLeaseHolderByIdQuery, Result<ResidentResponse>>
{
    public async Task<Result<ResidentResponse>> Handle(
        GetLeaseHolderByIdQuery request,
        CancellationToken cancellationToken)
    {
        var resident = await residentRepository.GetByIdWithContactsAsync(
            request.ResidentId,
            request.OrganizationId,
            cancellationToken);

        if (resident is null || resident.OrganizationId != request.OrganizationId)
        {
            return Result<ResidentResponse>.Failure(new Error(
                Code: "Tenant.NotFound",
                Message: "Lease-holding resident was not found.",
                Type: ErrorType.NotFound));
        }

        var isLeaseHolder = await residentRepository.IsLeaseHolderAsync(
            request.ResidentId,
            request.OrganizationId,
            cancellationToken);

        if (!isLeaseHolder)
        {
            return Result<ResidentResponse>.Failure(new Error(
                Code: "Tenant.NotFound",
                Message: "Resident does not hold a tenant role (PrimaryTenant or CoTenant) on any lease.",
                Type: ErrorType.NotFound));
        }

        return Result<ResidentResponse>.Success(resident.ToResponse());
    }
}
