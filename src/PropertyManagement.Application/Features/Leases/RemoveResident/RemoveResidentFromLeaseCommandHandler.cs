using MediatR;
using PropertyManagement.Application.Abstractions;
using PropertyManagement.Application.Abstractions.Repositories;
using PropertyManagement.Application.Common.Enums;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Application.Features.Leases.Responses;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Application.Features.Leases.RemoveResident;

public sealed class RemoveResidentFromLeaseCommandHandler(
    ILeaseRepository leaseRepository,
    IUnitOfWork unitOfWork,
    IClock clock) : IRequestHandler<RemoveResidentFromLeaseCommand, Result<LeaseResponse>>
{
    public async Task<Result<LeaseResponse>> Handle(RemoveResidentFromLeaseCommand request, CancellationToken cancellationToken)
    {
        var lease = await leaseRepository.GetByIdAsync(request.LeaseId, request.OrganizationId, cancellationToken);
        if (lease is null || lease.OrganizationId != request.OrganizationId)
        {
            return Result<LeaseResponse>.Failure(new Error(
                Code: "Lease.NotFound",
                Message: "The specified lease was not found.",
                Type: ErrorType.NotFound));
        }

        if (lease.Status != LeaseStatus.Draft)
        {
            return Result<LeaseResponse>.Failure(new Error(
                Code: "Lease.InvalidState",
                Message: $"Cannot remove residents from a lease in '{lease.Status}' state. Only Draft leases can be modified.",
                Type: ErrorType.Validation));
        }

        try
        {
            lease.RemoveResident(request.ResidentId, clock.UtcNow);
        }
        catch (InvalidOperationException ex)
        {
            return Result<LeaseResponse>.Failure(new Error(
                Code: "Lease.ResidentNotFound",
                Message: ex.Message,
                Type: ErrorType.NotFound));
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<LeaseResponse>.Success(LeaseResponse.FromEntity(lease));
    }
}
