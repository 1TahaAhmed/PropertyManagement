using MediatR;
using PropertyManagement.Application.Abstractions;
using PropertyManagement.Application.Abstractions.Repositories;
using PropertyManagement.Application.Common.Enums;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Application.Features.Leases.Responses;

namespace PropertyManagement.Application.Features.Leases.Submit;

public sealed class SubmitLeaseForApprovalCommandHandler(
    ILeaseRepository leaseRepository,
    IUnitOfWork unitOfWork,
    IClock clock) : IRequestHandler<SubmitLeaseForApprovalCommand, Result<LeaseResponse>>
{
    public async Task<Result<LeaseResponse>> Handle(SubmitLeaseForApprovalCommand request, CancellationToken cancellationToken)
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
            lease.SubmitForApproval(clock.UtcNow);
        }
        catch (InvalidOperationException ex)
        {
            return Result<LeaseResponse>.Failure(new Error(
                Code: "Lease.InvalidSubmission",
                Message: ex.Message,
                Type: ErrorType.Validation));
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<LeaseResponse>.Success(LeaseResponse.FromEntity(lease));
    }
}
