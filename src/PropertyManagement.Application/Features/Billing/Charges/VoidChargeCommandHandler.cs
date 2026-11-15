using System.Data;
using MediatR;
using PropertyManagement.Application.Abstractions;
using PropertyManagement.Application.Abstractions.Billing;
using PropertyManagement.Application.Abstractions.Repositories;
using PropertyManagement.Application.Common.Enums;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Application.Features.Billing.Responses;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Application.Features.Billing.Charges;

public sealed class VoidChargeCommandHandler(
    IChargeRepository chargeRepository,
    ILeaseRepository leaseRepository,
    IUnitOfWork unitOfWork,
    IClock clock) : IRequestHandler<VoidChargeCommand, Result<ChargeResponse>>
{
    public async Task<Result<ChargeResponse>> Handle(VoidChargeCommand request, CancellationToken cancellationToken)
    {
        var initialCharge = await chargeRepository.GetByIdAsync(request.ChargeId, request.OrganizationId, cancellationToken);
        if (initialCharge is null || initialCharge.OrganizationId != request.OrganizationId)
        {
            return Result<ChargeResponse>.Failure(new Error(
                Code: "Charge.NotFound",
                Message: "The specified charge was not found.",
                Type: ErrorType.NotFound));
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        await leaseRepository.AcquireUnitLockAsync(initialCharge.UnitId, request.OrganizationId, cancellationToken);

        chargeRepository.Detach(initialCharge);
        var charge = await chargeRepository.GetByIdAsync(request.ChargeId, request.OrganizationId, cancellationToken);
        if (charge is null || charge.OrganizationId != request.OrganizationId)
        {
            return Result<ChargeResponse>.Failure(new Error(
                Code: "Charge.NotFound",
                Message: "The specified charge was not found.",
                Type: ErrorType.NotFound));
        }

        if (!string.IsNullOrWhiteSpace(request.RowVersion))
        {
            try
            {
                chargeRepository.SetRowVersion(charge, Convert.FromBase64String(request.RowVersion));
            }
            catch (FormatException)
            {
                return Result<ChargeResponse>.Failure(new Error(
                    Code: "Charge.InvalidRowVersion",
                    Message: "The provided RowVersion concurrency token is invalid.",
                    Type: ErrorType.Validation));
            }
        }

        if (charge.Status != ChargeStatus.Pending)
        {
            return Result<ChargeResponse>.Failure(new Error(
                Code: "Charge.InvalidState",
                Message: $"Cannot void charge in '{charge.Status}' status. Only Pending charges can be voided directly.",
                Type: ErrorType.Conflict));
        }

        charge.Void(request.Reason, clock.UtcNow);
        chargeRepository.Update(charge);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result<ChargeResponse>.Success(ChargeResponse.FromEntity(charge));
    }
}
