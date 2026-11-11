using MediatR;
using PropertyManagement.Application.Abstractions;
using PropertyManagement.Application.Abstractions.Repositories;
using PropertyManagement.Application.Common.Enums;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Application.Features.Leases.Responses;
using PropertyManagement.Domain.Entities.Leases;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Application.Features.Leases.Renew;

public sealed class RenewLeaseCommandHandler(
    ILeaseRepository leaseRepository,
    IUnitOfWork unitOfWork,
    IClock clock) : IRequestHandler<RenewLeaseCommand, Result<LeaseResponse>>
{
    public async Task<Result<LeaseResponse>> Handle(RenewLeaseCommand request, CancellationToken cancellationToken)
    {
        var existingLease = await leaseRepository.GetByIdAsync(request.LeaseId, request.OrganizationId, cancellationToken);
        if (existingLease is null || existingLease.OrganizationId != request.OrganizationId)
        {
            return Result<LeaseResponse>.Failure(new Error(
                Code: "Lease.NotFound",
                Message: "The specified lease to renew was not found.",
                Type: ErrorType.NotFound));
        }

        var newStartDate = existingLease.EndDate.AddDays(1);

        // Verify no future overlapping lease exists
        var hasOverlap = await leaseRepository.HasOverlappingLeaseAsync(
            existingLease.UnitId,
            request.OrganizationId,
            newStartDate,
            request.NewEndDate,
            excludingLeaseId: existingLease.Id,
            cancellationToken: cancellationToken);

        if (hasOverlap)
        {
            return Result<LeaseResponse>.Failure(new Error(
                Code: "Lease.OverlapConflict",
                Message: "Cannot renew lease because another lease already exists for the renewal duration.",
                Type: ErrorType.Conflict));
        }

        LeaseFinancialTerms? newTerms = null;
        if (request.NewRentAmount.HasValue || request.NewSecurityDeposit.HasValue)
        {
            newTerms = new LeaseFinancialTerms(
                rentAmount: request.NewRentAmount ?? existingLease.RentAmount,
                securityDeposit: request.NewSecurityDeposit ?? existingLease.SecurityDeposit,
                frequency: request.NewFrequency ?? existingLease.Frequency,
                dueDayOfMonth: request.NewDueDayOfMonth ?? existingLease.DueDayOfMonth,
                gracePeriodDays: request.NewGracePeriodDays ?? existingLease.GracePeriodDays,
                lateFeePercentage: request.NewLateFeePercentage ?? existingLease.LateFeePercentage);
        }

        var utcNow = clock.UtcNow;
        Lease renewedLease;
        try
        {
            renewedLease = existingLease.CreateRenewal(request.NewEndDate, newTerms, utcNow);
        }
        catch (InvalidOperationException ex)
        {
            return Result<LeaseResponse>.Failure(new Error(
                Code: "Lease.RenewalError",
                Message: ex.Message,
                Type: ErrorType.Validation));
        }

        await leaseRepository.AddAsync(renewedLease, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<LeaseResponse>.Success(LeaseResponse.FromEntity(renewedLease));
    }
}
