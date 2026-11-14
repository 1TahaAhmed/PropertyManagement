using PropertyManagement.Domain.Common;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Domain.Entities.Billing.Services;

public interface IBillingCalculationEngine
{
    BillingCalculationResult Calculate(BillingCalculationRequest request);
    DateOnly CalculateDueDate(DateOnly billingPeriodStart, int dueDayOfMonth, DateOnly issueDate);
}

public sealed class BillingCalculationEngine : IBillingCalculationEngine
{
    public DateOnly CalculateDueDate(DateOnly billingPeriodStart, int dueDayOfMonth, DateOnly issueDate)
    {
        if (dueDayOfMonth is < 1 or > 31)
        {
            throw new ArgumentOutOfRangeException(nameof(dueDayOfMonth), "Due day of month must be between 1 and 31.");
        }

        var maxDayInMonth = DateTime.DaysInMonth(billingPeriodStart.Year, billingPeriodStart.Month);
        var clampedDay = Math.Min(dueDayOfMonth, maxDayInMonth);
        var candidateDueDate = new DateOnly(billingPeriodStart.Year, billingPeriodStart.Month, clampedDay);

        return candidateDueDate < issueDate ? issueDate : candidateDueDate;
    }

    public BillingCalculationResult Calculate(BillingCalculationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.RentAmount <= 0)
        {
            throw new ArgumentException("Rent amount must be greater than zero.", nameof(request));
        }

        if (!Enum.IsDefined(request.Frequency))
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Invalid rent frequency.");
        }

        if (request.DueDayOfMonth is < 1 or > 31)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Due day of month must be between 1 and 31.");
        }

        var currency = CurrencyCode.Normalize(request.Currency);

        var effectiveLeaseEnd = request.TerminationDate.HasValue && request.TerminationDate.Value < request.LeaseEndDate
            ? request.TerminationDate.Value
            : request.LeaseEndDate;

        if (effectiveLeaseEnd < request.LeaseStartDate)
        {
            throw new InvalidOperationException("Lease effective end date cannot be earlier than lease start date.");
        }

        var effectivePeriod = request.BillingPeriod.Intersect(request.LeaseStartDate, effectiveLeaseEnd)
            ?? throw new InvalidOperationException(
                $"Lease ({request.LeaseStartDate:yyyy-MM-dd} to {effectiveLeaseEnd:yyyy-MM-dd}) has no billable days within billing period ({request.BillingPeriod.StartDate:yyyy-MM-dd} to {request.BillingPeriod.EndDate:yyyy-MM-dd}).");

        var dueDate = request.DueDateOverride ?? CalculateDueDate(request.BillingPeriod.StartDate, request.DueDayOfMonth, request.IssueDate);
        if (dueDate < request.IssueDate)
        {
            throw new ArgumentException($"DueDate ({dueDate}) cannot be earlier than IssueDate ({request.IssueDate}).");
        }

        var lines = new List<CalculatedInvoiceLine>();
        var isProrated = false;

        if (request.IncludeRent)
        {
            var rentLines = CalculateRentLines(request.RentAmount, request.Frequency, request.BillingPeriod, effectivePeriod);
            lines.AddRange(rentLines);
            isProrated = rentLines.Any(l => l.IsProrated);
        }

        if (request.AdditionalCharges is { Count: > 0 })
        {
            foreach (var charge in request.AdditionalCharges)
            {
                if (!Enum.IsDefined(charge.Type))
                {
                    throw new ArgumentOutOfRangeException(nameof(request), $"Invalid additional charge type '{charge.Type}'.");
                }

                ArgumentException.ThrowIfNullOrWhiteSpace(charge.Description, nameof(charge.Description));
                if (charge.Quantity <= 0)
                {
                    throw new ArgumentException("Additional charge quantity must be greater than zero.", nameof(request));
                }

                if (charge.UnitPrice <= 0)
                {
                    throw new ArgumentException("Additional charge unit price must be greater than zero.", nameof(request));
                }

                var roundedUnitPrice = MoneyRounding.Round(charge.UnitPrice);
                var magnitude = MoneyRounding.Round(charge.Quantity * charge.UnitPrice);
                if (magnitude <= 0)
                {
                    throw new ArgumentException("Additional charge amount must be greater than zero after rounding.", nameof(request));
                }

                var signedAmount = Charge.IsCreditType(charge.Type) ? -magnitude : magnitude;

                lines.Add(new CalculatedInvoiceLine(
                    Type: charge.Type,
                    Description: charge.Description.Trim(),
                    Quantity: charge.Quantity,
                    UnitPrice: roundedUnitPrice,
                    Amount: signedAmount,
                    PeriodStart: charge.PeriodStart,
                    PeriodEnd: charge.PeriodEnd,
                    IsProrated: false,
                    OccupiedDays: null,
                    DaysInMonth: null,
                    SourceChargeId: charge.SourceChargeId));
            }
        }

        if (request.Discounts is { Count: > 0 })
        {
            foreach (var discount in request.Discounts)
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(discount.Description, nameof(discount.Description));
                var roundedAmount = MoneyRounding.Round(discount.Amount);
                if (roundedAmount <= 0)
                {
                    throw new ArgumentException("Discount amount must be greater than zero.", nameof(request));
                }

                lines.Add(new CalculatedInvoiceLine(
                    Type: ChargeType.Discount,
                    Description: discount.Description.Trim(),
                    Quantity: 1m,
                    UnitPrice: roundedAmount,
                    Amount: -roundedAmount,
                    PeriodStart: effectivePeriod.StartDate,
                    PeriodEnd: effectivePeriod.EndDate,
                    IsProrated: false,
                    OccupiedDays: null,
                    DaysInMonth: null,
                    SourceChargeId: discount.SourceChargeId));
            }
        }

        if (lines.Count == 0)
        {
            throw new InvalidOperationException("Billing calculation produced no invoice lines.");
        }

        var subtotal = MoneyRounding.Round(lines.Sum(l => l.Amount));
        if (subtotal < 0m)
        {
            throw new InvalidOperationException(
                $"Calculated invoice total ({subtotal:0.00}) cannot be negative. Discounts cannot exceed charges.");
        }

        return new BillingCalculationResult(
            BillingPeriod: request.BillingPeriod,
            EffectiveServicePeriod: effectivePeriod,
            IsProrated: isProrated,
            IssueDate: request.IssueDate,
            DueDate: dueDate,
            Currency: currency,
            Lines: lines.AsReadOnly(),
            Subtotal: subtotal,
            Total: subtotal);
    }

    private static List<CalculatedInvoiceLine> CalculateRentLines(
        decimal rentAmount,
        RentFrequency frequency,
        BillingPeriod billingPeriod,
        BillingPeriod effectivePeriod)
    {
        var cycleMonths = frequency switch
        {
            RentFrequency.Monthly => 1,
            RentFrequency.Quarterly => 3,
            RentFrequency.Yearly => 12,
            _ => throw new ArgumentOutOfRangeException(nameof(frequency), "Unsupported rent frequency.")
        };

        var isExactFullCycle =
            effectivePeriod.StartDate.Day == 1 &&
            effectivePeriod.StartDate == billingPeriod.StartDate &&
            effectivePeriod.EndDate == billingPeriod.EndDate &&
            effectivePeriod.EndDate == effectivePeriod.StartDate.AddMonths(cycleMonths).AddDays(-1);

        if (isExactFullCycle)
        {
            var roundedRent = MoneyRounding.Round(rentAmount);
            return
            [
                new CalculatedInvoiceLine(
                    Type: ChargeType.Rent,
                    Description: $"Rent ({effectivePeriod.StartDate:yyyy-MM-dd} to {effectivePeriod.EndDate:yyyy-MM-dd})",
                    Quantity: 1m,
                    UnitPrice: roundedRent,
                    Amount: roundedRent,
                    PeriodStart: effectivePeriod.StartDate,
                    PeriodEnd: effectivePeriod.EndDate,
                    IsProrated: false,
                    OccupiedDays: effectivePeriod.TotalDays,
                    DaysInMonth: cycleMonths == 1 ? DateTime.DaysInMonth(effectivePeriod.StartDate.Year, effectivePeriod.StartDate.Month) : null,
                    SourceChargeId: null)
            ];
        }

        var monthlyEquivalent = rentAmount / cycleMonths;
        var result = new List<CalculatedInvoiceLine>();

        var cursor = new DateOnly(effectivePeriod.StartDate.Year, effectivePeriod.StartDate.Month, 1);
        var lastMonth = new DateOnly(effectivePeriod.EndDate.Year, effectivePeriod.EndDate.Month, 1);

        while (cursor <= lastMonth)
        {
            var daysInMonth = DateTime.DaysInMonth(cursor.Year, cursor.Month);
            var monthStart = cursor;
            var monthEnd = new DateOnly(cursor.Year, cursor.Month, daysInMonth);

            var segmentStart = effectivePeriod.StartDate > monthStart ? effectivePeriod.StartDate : monthStart;
            var segmentEnd = effectivePeriod.EndDate < monthEnd ? effectivePeriod.EndDate : monthEnd;
            var occupiedDays = segmentEnd.DayNumber - segmentStart.DayNumber + 1;

            decimal lineAmount;
            bool segmentProrated;
            if (occupiedDays == daysInMonth)
            {
                lineAmount = MoneyRounding.Round(monthlyEquivalent);
                segmentProrated = false;
            }
            else
            {
                lineAmount = MoneyRounding.Round(monthlyEquivalent / daysInMonth * occupiedDays);
                segmentProrated = true;
            }

            if (lineAmount > 0m)
            {
                var description = segmentProrated
                    ? $"Prorated Rent ({segmentStart:yyyy-MM-dd} to {segmentEnd:yyyy-MM-dd}, {occupiedDays}/{daysInMonth} days)"
                    : $"Rent ({segmentStart:yyyy-MM-dd} to {segmentEnd:yyyy-MM-dd})";

                result.Add(new CalculatedInvoiceLine(
                    Type: ChargeType.Rent,
                    Description: description,
                    Quantity: 1m,
                    UnitPrice: lineAmount,
                    Amount: lineAmount,
                    PeriodStart: segmentStart,
                    PeriodEnd: segmentEnd,
                    IsProrated: segmentProrated,
                    OccupiedDays: occupiedDays,
                    DaysInMonth: daysInMonth,
                    SourceChargeId: null));
            }

            cursor = cursor.AddMonths(1);
        }

        return result;
    }
}
