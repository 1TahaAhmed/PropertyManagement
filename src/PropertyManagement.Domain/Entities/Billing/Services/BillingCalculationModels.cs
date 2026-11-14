using PropertyManagement.Domain.Common;
using PropertyManagement.Domain.Entities.Leases;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Domain.Entities.Billing.Services;

public sealed record BillingDiscountInput(
    string Description,
    decimal Amount,
    Guid? SourceChargeId = null);

public sealed record BillingAdditionalChargeInput(
    ChargeType Type,
    string Description,
    decimal Quantity,
    decimal UnitPrice,
    Guid? SourceChargeId = null,
    DateOnly? PeriodStart = null,
    DateOnly? PeriodEnd = null);

public sealed record BillingCalculationRequest(
    DateOnly LeaseStartDate,
    DateOnly LeaseEndDate,
    DateOnly? TerminationDate,
    decimal RentAmount,
    RentFrequency Frequency,
    int DueDayOfMonth,
    string Currency,
    BillingPeriod BillingPeriod,
    DateOnly IssueDate,
    DateOnly? DueDateOverride = null,
    IReadOnlyList<BillingDiscountInput>? Discounts = null,
    IReadOnlyList<BillingAdditionalChargeInput>? AdditionalCharges = null,
    bool IncludeRent = true)
{
    public static BillingCalculationRequest FromLease(
        Lease lease,
        BillingPeriod billingPeriod,
        DateOnly issueDate,
        DateOnly? dueDateOverride = null,
        IReadOnlyList<BillingDiscountInput>? discounts = null,
        IReadOnlyList<BillingAdditionalChargeInput>? additionalCharges = null,
        bool includeRent = true)
    {
        ArgumentNullException.ThrowIfNull(lease);

        return new BillingCalculationRequest(
            LeaseStartDate: lease.StartDate,
            LeaseEndDate: lease.EndDate,
            TerminationDate: lease.TerminationDetails?.TerminationDate,
            RentAmount: lease.RentAmount,
            Frequency: lease.Frequency,
            DueDayOfMonth: lease.DueDayOfMonth,
            Currency: lease.Currency,
            BillingPeriod: billingPeriod,
            IssueDate: issueDate,
            DueDateOverride: dueDateOverride,
            Discounts: discounts,
            AdditionalCharges: additionalCharges,
            IncludeRent: includeRent);
    }
}

public sealed record CalculatedInvoiceLine(
    ChargeType Type,
    string Description,
    decimal Quantity,
    decimal UnitPrice,
    decimal Amount,
    DateOnly? PeriodStart,
    DateOnly? PeriodEnd,
    bool IsProrated,
    int? OccupiedDays,
    int? DaysInMonth,
    Guid? SourceChargeId);

public sealed record BillingCalculationResult(
    BillingPeriod BillingPeriod,
    BillingPeriod EffectiveServicePeriod,
    bool IsProrated,
    DateOnly IssueDate,
    DateOnly DueDate,
    string Currency,
    IReadOnlyList<CalculatedInvoiceLine> Lines,
    decimal Subtotal,
    decimal Total)
{
    public Invoice ToDraftInvoice(
        Guid organizationId,
        Guid leaseId,
        Guid propertyId,
        Guid unitId,
        Guid residentId,
        DateTime utcNow,
        string? notes = null)
    {
        var invoice = Invoice.CreateDraft(
            organizationId: organizationId,
            leaseId: leaseId,
            propertyId: propertyId,
            unitId: unitId,
            residentId: residentId,
            billingPeriod: BillingPeriod,
            issueDate: IssueDate,
            dueDate: DueDate,
            currency: Currency,
            utcNow: utcNow,
            notes: notes);

        foreach (var line in Lines)
        {
            invoice.AddLine(
                type: line.Type,
                description: line.Description,
                quantity: line.Quantity,
                unitPrice: line.UnitPrice,
                utcNow: utcNow,
                sourceChargeId: line.SourceChargeId,
                periodStart: line.PeriodStart,
                periodEnd: line.PeriodEnd);
        }

        return invoice;
    }
}
