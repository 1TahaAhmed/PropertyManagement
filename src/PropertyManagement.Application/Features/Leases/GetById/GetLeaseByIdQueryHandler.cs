using MediatR;
using PropertyManagement.Application.Abstractions.Repositories;
using PropertyManagement.Application.Common.Enums;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Application.Features.Leases.Responses;

namespace PropertyManagement.Application.Features.Leases.GetById;

public sealed class GetLeaseByIdQueryHandler(ILeaseRepository leaseRepository)
    : IRequestHandler<GetLeaseByIdQuery, Result<LeaseResponse>>
{
    public async Task<Result<LeaseResponse>> Handle(GetLeaseByIdQuery request, CancellationToken cancellationToken)
    {
        var lease = await leaseRepository.GetByIdAsync(request.LeaseId, request.OrganizationId, cancellationToken);
        if (lease is null || lease.OrganizationId != request.OrganizationId)
        {
            return Result<LeaseResponse>.Failure(new Error(
                Code: "Lease.NotFound",
                Message: "The specified lease was not found.",
                Type: ErrorType.NotFound));
        }

        return Result<LeaseResponse>.Success(LeaseResponse.FromEntity(lease));
    }
}
