using System.Data;
using MediatR;
using PropertyManagement.Application.Abstractions;
using PropertyManagement.Application.Abstractions.Repositories;
using PropertyManagement.Application.Common.Enums;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Application.Features.Leases.Responses;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Application.Features.Leases.Approve;

public sealed class ApproveLeaseCommandHandler(
    ILeaseRepository leaseRepository,
    IUnitOfWork unitOfWork,
    IClock clock) : IRequestHandler<ApproveLeaseCommand, Result<LeaseResponse>>
{
    public async Task<Result<LeaseResponse>> Handle(ApproveLeaseCommand request, CancellationToken cancellationToken)
    {
        var lease = await leaseRepository.GetByIdAsync(request.LeaseId, request.OrganizationId, cancellationToken);
        if (lease is null || lease.OrganizationId != request.OrganizationId)
        {
            return Result<LeaseResponse>.Failure(new Error(
                Code: "Lease.NotFound",
                Message: "The specified lease was not found.",
                Type: ErrorType.NotFound));
        }

        if (lease.Status != LeaseStatus.PendingApproval)
        {
            return Result<LeaseResponse>.Failure(new Error(
                Code: "Lease.InvalidState",
                Message: $"Cannot approve lease in '{lease.Status}' state. Only PendingApproval leases can be approved.",
                Type: ErrorType.Validation));
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        await leaseRepository.AcquireUnitLockAsync(lease.UnitId, request.OrganizationId, cancellationToken);

        // Re-verify no overlapping active lease before activation under Serializable + Unit UPDLOCK
        var hasOverlap = await leaseRepository.HasOverlappingLeaseAsync(
            lease.UnitId,
            request.OrganizationId,
            lease.StartDate,
            lease.EndDate,
            excludingLeaseId: lease.Id,
            cancellationToken: cancellationToken);

        if (hasOverlap)
        {
            return Result<LeaseResponse>.Failure(new Error(
                Code: "Lease.OverlapConflict",
                Message: "Cannot activate lease because another active lease overlaps with this duration.",
                Type: ErrorType.Conflict));
        }

        var utcNow = clock.UtcNow;

        try
        {
            lease.Approve(utcNow);
        }
        catch (InvalidOperationException ex)
        {
            return Result<LeaseResponse>.Failure(new Error(
                Code: "Lease.ApprovalError",
                Message: ex.Message,
                Type: ErrorType.Validation));
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result<LeaseResponse>.Success(LeaseResponse.FromEntity(lease));
    }
}
