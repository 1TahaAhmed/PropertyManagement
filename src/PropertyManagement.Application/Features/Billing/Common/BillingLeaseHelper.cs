using PropertyManagement.Application.Abstractions.Billing;
using PropertyManagement.Application.Common.Enums;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Domain.Entities.Billing;
using PropertyManagement.Domain.Entities.Billing.Services;
using PropertyManagement.Domain.Entities.Leases;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Application.Features.Billing.Common;

internal static class BillingLeaseHelper
{
    public static bool IsBillableStatus(LeaseStatus status) =>
        status is LeaseStatus.Active or LeaseStatus.Terminated or LeaseStatus.Expired;

    public static string BuildRentIdempotencyKey(Guid leaseId, DateOnly periodStart, DateOnly periodEnd) =>
        $"RENT:{leaseId:N}:{periodStart:yyyyMMdd}:{periodEnd:yyyyMMdd}";

    public static Result<Guid> ResolveBillableResidentId(Lease lease)
    {
        var resident = lease.Residents.FirstOrDefault(r => r.Role == LeaseResidentRole.PrimaryTenant)
            ?? lease.Residents.FirstOrDefault(r => r.IsFinanciallyResponsible);

        if (resident is null)
        {
            return Result<Guid>.Failure(new Error(
                Code: "Lease.NoPrimaryTenant",
                Message: "The lease does not have a financially responsible resident assigned.",
                Type: ErrorType.Validation));
        }

        return Result<Guid>.Success(resident.ResidentId);
    }

    public static async Task<Result<IReadOnlyList<Charge>>> ResolvePendingChargesAsync(
        IChargeRepository chargeRepository,
        Lease lease,
        Guid organizationId,
        bool includePendingCharges,
        IReadOnlyCollection<Guid>? explicitChargeIds,
        DateOnly periodEnd,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<Charge> charges;

        if (explicitChargeIds is { Count: > 0 })
        {
            var distinctIds = explicitChargeIds.Where(id => id != Guid.Empty).Distinct().ToList();
            charges = await chargeRepository.GetByIdsAsync(distinctIds, organizationId, cancellationToken);

            if (charges.Count != distinctIds.Count)
            {
                return Result<IReadOnlyList<Charge>>.Failure(new Error(
                    Code: "Charge.NotFound",
                    Message: "One or more specified charges were not found in your organization.",
                    Type: ErrorType.NotFound));
            }

            foreach (var charge in charges)
            {
                if (charge.LeaseId != lease.Id)
                {
                    return Result<IReadOnlyList<Charge>>.Failure(new Error(
                        Code: "Charge.LeaseMismatch",
                        Message: $"Charge '{charge.Id}' does not belong to lease '{lease.Id}'.",
                        Type: ErrorType.Validation));
                }

                if (charge.Status != ChargeStatus.Pending)
                {
                    return Result<IReadOnlyList<Charge>>.Failure(new Error(
                        Code: "Charge.NotPending",
                        Message: $"Charge '{charge.Id}' is in '{charge.Status}' status and cannot be billed.",
                        Type: ErrorType.Conflict));
                }
            }
        }
        else if (includePendingCharges)
        {
            charges = await chargeRepository.GetPendingByLeaseAsync(lease.Id, organizationId, periodEnd, cancellationToken);
        }
        else
        {
            charges = [];
        }

        foreach (var charge in charges)
        {
            if (!string.Equals(charge.Currency, lease.Currency, StringComparison.OrdinalIgnoreCase))
            {
                return Result<IReadOnlyList<Charge>>.Failure(new Error(
                    Code: "Charge.CurrencyMismatch",
                    Message: $"Charge '{charge.Id}' currency ({charge.Currency}) does not match lease currency ({lease.Currency}).",
                    Type: ErrorType.Validation));
            }
        }

        return Result<IReadOnlyList<Charge>>.Success(charges);
    }

    public static (List<BillingDiscountInput> Discounts, List<BillingAdditionalChargeInput> AdditionalCharges) MapChargesAndInputs(
        IReadOnlyList<Charge> pendingCharges,
        IReadOnlyList<BillingDiscountRequest>? discounts,
        IReadOnlyList<BillingAdditionalChargeRequest>? additionalCharges)
    {
        var discountInputs = new List<BillingDiscountInput>();
        var additionalInputs = new List<BillingAdditionalChargeInput>();

        foreach (var charge in pendingCharges)
        {
            if (charge.Type == ChargeType.Discount)
            {
                discountInputs.Add(new BillingDiscountInput(
                    Description: charge.Description,
                    Amount: Math.Abs(charge.Amount),
                    SourceChargeId: charge.Id));
            }
            else
            {
                additionalInputs.Add(new BillingAdditionalChargeInput(
                    Type: charge.Type,
                    Description: charge.Description,
                    Quantity: charge.Quantity,
                    UnitPrice: charge.UnitPrice,
                    SourceChargeId: charge.Id,
                    PeriodStart: charge.PeriodStart,
                    PeriodEnd: charge.PeriodEnd));
            }
        }

        if (discounts is { Count: > 0 })
        {
            foreach (var d in discounts)
            {
                discountInputs.Add(new BillingDiscountInput(d.Description, d.Amount));
            }
        }

        if (additionalCharges is { Count: > 0 })
        {
            foreach (var c in additionalCharges)
            {
                additionalInputs.Add(new BillingAdditionalChargeInput(
                    Type: c.Type,
                    Description: c.Description,
                    Quantity: c.Quantity,
                    UnitPrice: c.UnitPrice,
                    SourceChargeId: null,
                    PeriodStart: c.PeriodStart,
                    PeriodEnd: c.PeriodEnd));
            }
        }

        return (discountInputs, additionalInputs);
    }
}
