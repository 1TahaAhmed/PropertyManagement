using MediatR;
using PropertyManagement.Application.Abstractions;
using PropertyManagement.Application.Abstractions.Repositories;
using PropertyManagement.Application.Abstractions.Residents;
using PropertyManagement.Application.Common.Enums;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Application.Features.Leases.Responses;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Application.Features.Leases.AddResident;

public sealed class AddResidentToLeaseCommandHandler(
    ILeaseRepository leaseRepository,
    IResidentRepository residentRepository,
    IUnitOfWork unitOfWork,
    IClock clock) : IRequestHandler<AddResidentToLeaseCommand, Result<LeaseResponse>>
{
    public async Task<Result<LeaseResponse>> Handle(AddResidentToLeaseCommand request, CancellationToken cancellationToken)
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
                Message: $"Cannot add residents to a lease in '{lease.Status}' state. Residents can only be added to Draft leases.",
                Type: ErrorType.Validation));
        }

        var resident = await residentRepository.GetByIdAsync(request.ResidentId, request.OrganizationId, cancellationToken);
        if (resident is null || resident.OrganizationId != request.OrganizationId)
        {
            return Result<LeaseResponse>.Failure(new Error(
                Code: "Resident.NotFound",
                Message: "The specified resident does not exist in your organization.",
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
                Message: "The resident must be in Active status to be added to a lease.",
                Type: ErrorType.Validation));
        }

        try
        {
            lease.AddResident(request.ResidentId, request.Role, clock.UtcNow);
        }
        catch (InvalidOperationException ex)
        {
            return Result<LeaseResponse>.Failure(new Error(
                Code: "Lease.ResidentAssignmentError",
                Message: ex.Message,
                Type: ErrorType.Validation));
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<LeaseResponse>.Success(LeaseResponse.FromEntity(lease));
    }
}
