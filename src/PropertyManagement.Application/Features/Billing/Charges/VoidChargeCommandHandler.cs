using MediatR;
using PropertyManagement.Application.Abstractions;
using PropertyManagement.Application.Abstractions.Billing;
using PropertyManagement.Application.Common.Enums;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Application.Features.Billing.Responses;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Application.Features.Billing.Charges;

public sealed class VoidChargeCommandHandler(
    IChargeRepository chargeRepository,
    IUnitOfWork unitOfWork,
    IClock clock) : IRequestHandler<VoidChargeCommand, Result<ChargeResponse>>
{
    public async Task<Result<ChargeResponse>> Handle(VoidChargeCommand request, CancellationToken cancellationToken)
    {
        var charge = await chargeRepository.GetByIdAsync(request.ChargeId, request.OrganizationId, cancellationToken);
        if (charge is null || charge.OrganizationId != request.OrganizationId)
        {
            return Result<ChargeResponse>.Failure(new Error(
                Code: "Charge.NotFound",
                Message: "The specified charge was not found.",
                Type: ErrorType.NotFound));
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

        return Result<ChargeResponse>.Success(ChargeResponse.FromEntity(charge));
    }
}
