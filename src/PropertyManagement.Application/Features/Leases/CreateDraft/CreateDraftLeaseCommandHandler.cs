using System.Data;
using MediatR;
using PropertyManagement.Application.Abstractions;
using PropertyManagement.Application.Abstractions.Properties;
using PropertyManagement.Application.Abstractions.Repositories;
using PropertyManagement.Application.Abstractions.Residents;
using PropertyManagement.Application.Common.Enums;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Application.Features.Leases.Responses;
using PropertyManagement.Domain.Entities.Leases;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Application.Features.Leases.CreateDraft;

public sealed class CreateDraftLeaseCommandHandler(
    IPropertyRepository propertyRepository,
    IUnitRepository unitRepository,
    IResidentRepository residentRepository,
    ILeaseRepository leaseRepository,
    IUnitOfWork unitOfWork,
    IClock clock) : IRequestHandler<CreateDraftLeaseCommand, Result<LeaseResponse>>
{
    public async Task<Result<LeaseResponse>> Handle(CreateDraftLeaseCommand request, CancellationToken cancellationToken)
    {
        var property = await propertyRepository.GetByIdAsync(request.PropertyId, request.OrganizationId, cancellationToken);
        if (property is null || property.OrganizationId != request.OrganizationId)
        {
            return Result<LeaseResponse>.Failure(new Error(
                Code: "Property.NotFound",
                Message: "The specified property does not exist in your organization.",
                Type: ErrorType.NotFound));
        }

        if (property.Status == PropertyStatus.Archived)
        {
            return Result<LeaseResponse>.Failure(new Error(
                Code: "Property.Archived",
                Message: "Cannot create leases for an archived property.",
                Type: ErrorType.Validation));
        }

        var unit = await unitRepository.GetByIdAsync(request.UnitId, request.OrganizationId, cancellationToken);
        if (unit is null || unit.OrganizationId != request.OrganizationId || unit.PropertyId != request.PropertyId)
        {
            return Result<LeaseResponse>.Failure(new Error(
                Code: "Unit.NotFound",
                Message: "The specified unit does not exist in the selected property.",
                Type: ErrorType.NotFound));
        }

        if (unit.Status != UnitOperationalStatus.Active)
        {
            return Result<LeaseResponse>.Failure(new Error(
                Code: "Unit.NotActive",
                Message: $"Cannot create a lease on a unit with status '{unit.Status}'. Unit must be Active.",
                Type: ErrorType.Validation));
        }

        if (request.PrimaryResidentId.HasValue && request.PrimaryResidentId.Value != Guid.Empty)
        {
            var resident = await residentRepository.GetByIdAsync(request.PrimaryResidentId.Value, request.OrganizationId, cancellationToken);
            if (resident is null || resident.OrganizationId != request.OrganizationId)
            {
                return Result<LeaseResponse>.Failure(new Error(
                    Code: "Resident.NotFound",
                    Message: "The specified primary resident does not exist in your organization.",
                    Type: ErrorType.NotFound));
            }

            if (resident.Status == ResidentStatus.Blacklisted)
            {
                return Result<LeaseResponse>.Failure(new Error(
                    Code: "Resident.Blacklisted",
                    Message: "Cannot assign a blacklisted resident to a lease.",
                    Type: ErrorType.Validation));
            }

            if (!resident.IsEligibleForLease)
            {
                return Result<LeaseResponse>.Failure(new Error(
                    Code: "Resident.NotActive",
                    Message: "The primary resident must be in Active status to be added to a lease.",
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

        var utcNow = clock.UtcNow;
        var lease = Lease.CreateDraft(
            organizationId: request.OrganizationId,
            propertyId: request.PropertyId,
            unitId: request.UnitId,
            startDate: request.StartDate,
            endDate: request.EndDate,
            terms: terms,
            utcNow: utcNow);

        if (request.PrimaryResidentId.HasValue && request.PrimaryResidentId.Value != Guid.Empty)
        {
            lease.AddResident(request.PrimaryResidentId.Value, LeaseResidentRole.PrimaryTenant, utcNow);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        await leaseRepository.AcquireUnitLockAsync(request.UnitId, request.OrganizationId, cancellationToken);

        var hasOverlap = await leaseRepository.HasOverlappingLeaseAsync(
            request.UnitId,
            request.OrganizationId,
            request.StartDate,
            request.EndDate,
            cancellationToken: cancellationToken);

        if (hasOverlap)
        {
            return Result<LeaseResponse>.Failure(new Error(
                Code: "Lease.OverlapConflict",
                Message: "A lease already exists that overlaps with the requested date range for this unit.",
                Type: ErrorType.Conflict));
        }

        await leaseRepository.AddAsync(lease, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result<LeaseResponse>.Success(LeaseResponse.FromEntity(lease));
    }
}
