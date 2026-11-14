using MediatR;
using PropertyManagement.Application.Abstractions;
using PropertyManagement.Application.Abstractions.Repositories;
using PropertyManagement.Application.Common.Enums;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Application.Features.Leases.Responses;

namespace PropertyManagement.Application.Features.Leases.Cancel;

public sealed class CancelLeaseCommandHandler(
    ILeaseRepository leaseRepository,
    IUnitOfWork unitOfWork,
    IClock clock) : IRequestHandler<CancelLeaseCommand, Result<LeaseResponse>>
{
    public async Task<Result<LeaseResponse>> Handle(CancelLeaseCommand request, CancellationToken cancellationToken)
    {
        var lease = await leaseRepository.GetByIdAsync(request.LeaseId, request.OrganizationId, cancellationToken);
        if (lease is null || lease.OrganizationId != request.OrganizationId)
        {
            return Result<LeaseResponse>.Failure(new Error(
                Code: "Lease.NotFound",
                Message: "The specified lease was not found.",
                Type: ErrorType.NotFound));
        }

        try
        {
            lease.Cancel(clock.UtcNow);
        }
        catch (InvalidOperationException ex)
        {
            return Result<LeaseResponse>.Failure(new Error(
                Code: "Lease.CancelError",
                Message: ex.Message,
                Type: ErrorType.Validation));
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<LeaseResponse>.Success(LeaseResponse.FromEntity(lease));
    }
}
