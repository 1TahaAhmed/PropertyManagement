using System.Data;
using MediatR;
using PropertyManagement.Application.Abstractions;
using PropertyManagement.Application.Abstractions.Repositories;
using PropertyManagement.Application.Common.Enums;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Application.Features.Leases.Responses;
using PropertyManagement.Domain.Entities.Leases;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Application.Features.Leases.UpdateDraft;

public sealed class UpdateDraftLeaseCommandHandler(
    ILeaseRepository leaseRepository,
    IUnitOfWork unitOfWork,
    IClock clock) : IRequestHandler<UpdateDraftLeaseCommand, Result<LeaseResponse>>
{
    public async Task<Result<LeaseResponse>> Handle(UpdateDraftLeaseCommand request, CancellationToken cancellationToken)
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
                Message: $"Cannot update terms of lease in '{lease.Status}' state. Only Draft leases can be updated.",
                Type: ErrorType.Validation));
        }

        if (!string.IsNullOrWhiteSpace(request.RowVersion))
        {
            try
            {
                leaseRepository.SetRowVersion(lease, Convert.FromBase64String(request.RowVersion));
            }
            catch (FormatException)
            {
                return Result<LeaseResponse>.Failure(new Error(
                    Code: "Lease.InvalidRowVersion",
                    Message: "The provided RowVersion concurrency token is invalid.",
                    Type: ErrorType.Validation));
            }
        }

        var terms = new LeaseFinancialTerms(
            request.RentAmount,
            request.SecurityDeposit,
            request.Frequency,
            request.DueDayOfMonth,
            request.GracePeriodDays,
            request.LateFeePercentage);

        await using var transaction = await unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        await leaseRepository.AcquireUnitLockAsync(lease.UnitId, request.OrganizationId, cancellationToken);

        var hasOverlap = await leaseRepository.HasOverlappingLeaseAsync(
            lease.UnitId,
            request.OrganizationId,
            request.StartDate,
            request.EndDate,
            excludingLeaseId: lease.Id,
            cancellationToken: cancellationToken);

        if (hasOverlap)
        {
            return Result<LeaseResponse>.Failure(new Error(
                Code: "Lease.OverlapConflict",
                Message: "Another lease already exists that overlaps with the requested date range for this unit.",
                Type: ErrorType.Conflict));
        }

        try
        {
            lease.UpdateDraftTerms(request.StartDate, request.EndDate, terms, clock.UtcNow);
        }
        catch (ArgumentException ex)
        {
            return Result<LeaseResponse>.Failure(new Error(
                Code: "Lease.ValidationError",
                Message: ex.Message,
                Type: ErrorType.Validation));
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result<LeaseResponse>.Success(LeaseResponse.FromEntity(lease));
    }
}
