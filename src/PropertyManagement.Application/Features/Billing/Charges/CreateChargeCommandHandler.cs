using MediatR;
using PropertyManagement.Application.Abstractions;
using PropertyManagement.Application.Abstractions.Billing;
using PropertyManagement.Application.Abstractions.Repositories;
using PropertyManagement.Application.Common.Enums;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Application.Features.Billing.Common;
using PropertyManagement.Application.Features.Billing.Responses;
using PropertyManagement.Domain.Entities.Billing;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Application.Features.Billing.Charges;

public sealed class CreateChargeCommandHandler(
    ILeaseRepository leaseRepository,
    IChargeRepository chargeRepository,
    IUnitOfWork unitOfWork,
    IClock clock) : IRequestHandler<CreateChargeCommand, Result<ChargeResponse>>
{
    public async Task<Result<ChargeResponse>> Handle(CreateChargeCommand request, CancellationToken cancellationToken)
    {
        var lease = await leaseRepository.GetByIdAsync(request.LeaseId, request.OrganizationId, cancellationToken);
        if (lease is null || lease.OrganizationId != request.OrganizationId)
        {
            return Result<ChargeResponse>.Failure(new Error(
                Code: "Lease.NotFound",
                Message: "The specified lease was not found.",
                Type: ErrorType.NotFound));
        }

        if (lease.Status == LeaseStatus.Cancelled)
        {
            return Result<ChargeResponse>.Failure(new Error(
                Code: "Lease.Cancelled",
                Message: "Cannot add charges to a cancelled lease.",
                Type: ErrorType.Validation));
        }

        var residentResult = BillingLeaseHelper.ResolveBillableResidentId(lease);
        if (residentResult.IsFailure)
        {
            return Result<ChargeResponse>.Failure(residentResult.Errors.ToArray());
        }

        Charge charge;
        try
        {
            charge = Charge.Create(
                organizationId: request.OrganizationId,
                leaseId: lease.Id,
                propertyId: lease.PropertyId,
                unitId: lease.UnitId,
                residentId: residentResult.Value,
                type: request.Type,
                description: request.Description,
                quantity: request.Quantity,
                unitPrice: request.UnitPrice,
                currency: lease.Currency,
                utcNow: clock.UtcNow,
                periodStart: request.PeriodStart,
                periodEnd: request.PeriodEnd);
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            return Result<ChargeResponse>.Failure(new Error(
                Code: "Charge.ValidationError",
                Message: ex.Message,
                Type: ErrorType.Validation));
        }

        await chargeRepository.AddAsync(charge, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<ChargeResponse>.Success(ChargeResponse.FromEntity(charge));
    }
}
