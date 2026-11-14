using MediatR;
using PropertyManagement.Application.Abstractions;
using PropertyManagement.Application.Abstractions.Repositories;
using PropertyManagement.Application.Common.Enums;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Application.Features.Leases.Responses;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Application.Features.Leases.ChangeResidentRole;

public sealed class ChangeLeaseResidentRoleCommandHandler(
    ILeaseRepository leaseRepository,
    IUnitOfWork unitOfWork,
    IClock clock) : IRequestHandler<ChangeLeaseResidentRoleCommand, Result<LeaseResponse>>
{
    public async Task<Result<LeaseResponse>> Handle(ChangeLeaseResidentRoleCommand request, CancellationToken cancellationToken)
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
                Message: $"Cannot change resident roles on a lease in '{lease.Status}' state. Only Draft leases can be modified.",
                Type: ErrorType.Validation));
        }

        try
        {
            lease.ChangeResidentRole(request.ResidentId, request.NewRole, clock.UtcNow);
        }
        catch (InvalidOperationException ex)
        {
            var errorType = ex.Message.Contains("was not found", StringComparison.OrdinalIgnoreCase)
                ? ErrorType.NotFound
                : ErrorType.Validation;
            return Result<LeaseResponse>.Failure(new Error(
                Code: "Lease.ResidentRoleChangeError",
                Message: ex.Message,
                Type: errorType));
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<LeaseResponse>.Success(LeaseResponse.FromEntity(lease));
    }
}
