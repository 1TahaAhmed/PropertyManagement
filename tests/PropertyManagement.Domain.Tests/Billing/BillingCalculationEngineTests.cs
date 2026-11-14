using PropertyManagement.Domain.Common;
using PropertyManagement.Domain.Entities.Billing;
using PropertyManagement.Domain.Entities.Billing.Services;
using PropertyManagement.Domain.Entities.Leases;
using PropertyManagement.Domain.Entities.Organizations;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Domain.Tests.Billing;

public sealed class BillingCalculationEngineTests
{
    private readonly BillingCalculationEngine _engine = new();

    [Fact]
    public void Calculate_FullMonthRent_ShouldReturnExactFullAmountWithoutProration()
    {
        var request = new BillingCalculationRequest(
            LeaseStartDate: new DateOnly(2026, 1, 1),
            LeaseEndDate: new DateOnly(2026, 12, 31),
            TerminationDate: null,
            RentAmount: 10000m,
            Frequency: RentFrequency.Monthly,
            DueDayOfMonth: 5,
            Currency: "USD",
            BillingPeriod: BillingPeriod.ForMonth(2026, 3),
            IssueDate: new DateOnly(2026, 2, 25));

        var result = _engine.Calculate(request);

        Assert.False(result.IsProrated);
        Assert.Equal(new DateOnly(2026, 3, 5), result.DueDate);
        Assert.Single(result.Lines);
        Assert.Equal(10000.00m, result.Total);
    }

    [Fact]
    public void Calculate_MidMonthMoveIn_ShouldProrateUsingActualCalendarDaysInMonth()
    {
        var request = new BillingCalculationRequest(
            LeaseStartDate: new DateOnly(2026, 3, 15),
            LeaseEndDate: new DateOnly(2027, 3, 14),
            TerminationDate: null,
            RentAmount: 10000m,
            Frequency: RentFrequency.Monthly,
            DueDayOfMonth: 5,
            Currency: "USD",
            BillingPeriod: BillingPeriod.ForMonth(2026, 3),
            IssueDate: new DateOnly(2026, 3, 15));

        var result = _engine.Calculate(request);

        Assert.True(result.IsProrated);
        Assert.Equal(new DateOnly(2026, 3, 15), result.EffectiveServicePeriod.StartDate);
        Assert.Equal(new DateOnly(2026, 3, 31), result.EffectiveServicePeriod.EndDate);
        Assert.Equal(new DateOnly(2026, 3, 15), result.DueDate);
        var line = Assert.Single(result.Lines);
        Assert.Equal(17, line.OccupiedDays);
        Assert.Equal(31, line.DaysInMonth);
        Assert.Equal(5483.87m, result.Total);
    }

    [Fact]
    public void Calculate_MoveOutOrTerminationMidMonth_ShouldProrateToTerminationDate()
    {
        var request = new BillingCalculationRequest(
            LeaseStartDate: new DateOnly(2025, 6, 1),
            LeaseEndDate: new DateOnly(2026, 12, 31),
            TerminationDate: new DateOnly(2026, 3, 10),
            RentAmount: 10000m,
            Frequency: RentFrequency.Monthly,
            DueDayOfMonth: 1,
            Currency: "USD",
            BillingPeriod: BillingPeriod.ForMonth(2026, 3),
            IssueDate: new DateOnly(2026, 3, 1));

        var result = _engine.Calculate(request);

        Assert.True(result.IsProrated);
        Assert.Equal(new DateOnly(2026, 3, 1), result.EffectiveServicePeriod.StartDate);
        Assert.Equal(new DateOnly(2026, 3, 10), result.EffectiveServicePeriod.EndDate);
        Assert.Equal(3225.81m, result.Total);
    }

    [Fact]
    public void Calculate_StartAndEndWithinSameMonth_ShouldProrateExactOccupiedDays()
    {
        var request = new BillingCalculationRequest(
            LeaseStartDate: new DateOnly(2026, 3, 10),
            LeaseEndDate: new DateOnly(2026, 3, 20),
            TerminationDate: null,
            RentAmount: 10000m,
            Frequency: RentFrequency.Monthly,
            DueDayOfMonth: 10,
            Currency: "USD",
            BillingPeriod: BillingPeriod.ForMonth(2026, 3),
            IssueDate: new DateOnly(2026, 3, 10));

        var result = _engine.Calculate(request);

        Assert.True(result.IsProrated);
        var line = Assert.Single(result.Lines);
        Assert.Equal(11, line.OccupiedDays);
        Assert.Equal(3548.39m, result.Total);
    }

    [Fact]
    public void Calculate_FebruaryNonLeapAndLeapYear_ShouldRespect28And29Days()
    {
        var nonLeap = _engine.Calculate(new BillingCalculationRequest(
            new DateOnly(2027, 2, 15),
            new DateOnly(2028, 2, 14),
            null, 10000m, RentFrequency.Monthly, 1, "USD",
            BillingPeriod.ForMonth(2027, 2),
            new DateOnly(2027, 2, 15)));

        Assert.Equal(28, nonLeap.Lines.Single().DaysInMonth);
        Assert.Equal(5000.00m, nonLeap.Total);

        var leap = _engine.Calculate(new BillingCalculationRequest(
            new DateOnly(2028, 2, 15),
            new DateOnly(2029, 2, 14),
            null, 10000m, RentFrequency.Monthly, 1, "USD",
            BillingPeriod.ForMonth(2028, 2),
            new DateOnly(2028, 2, 15)));

        Assert.Equal(29, leap.Lines.Single().DaysInMonth);
        Assert.Equal(5172.41m, leap.Total);
    }

    [Theory]
    [InlineData(2027, 2, 31, 2027, 2, 28)]
    [InlineData(2028, 2, 31, 2028, 2, 29)]
    [InlineData(2026, 4, 31, 2026, 4, 30)]
    [InlineData(2026, 3, 31, 2026, 3, 31)]
    public void CalculateDueDate_WhenDueDayExceedsMonthLength_ShouldClampToLastValidDayOfMonth(
        int year, int month, int dueDay, int expectedYear, int expectedMonth, int expectedDay)
    {
        var periodStart = new DateOnly(year, month, 1);
        var issueDate = periodStart;

        var dueDate = _engine.CalculateDueDate(periodStart, dueDay, issueDate);

        Assert.Equal(new DateOnly(expectedYear, expectedMonth, expectedDay), dueDate);
    }

    [Fact]
    public void Calculate_WithDiscountsAndAdditionalCharges_ShouldProduceTransparentLinesAndDraftInvoice()
    {
        var lease = Lease.CreateDraft(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 12, 31),
            new LeaseFinancialTerms(10000m, 20000m, RentFrequency.Monthly, dueDayOfMonth: 5, currency: "EGP"),
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        var request = BillingCalculationRequest.FromLease(
            lease,
            BillingPeriod.ForMonth(2026, 4),
            new DateOnly(2026, 3, 25),
            discounts: [new BillingDiscountInput("Early-bird discount", 1000m)],
            additionalCharges: [new BillingAdditionalChargeInput(ChargeType.Parking, "Garage spot #12", 1m, 500m)]);

        var result = _engine.Calculate(request);

        Assert.Equal("EGP", result.Currency);
        Assert.Equal(3, result.Lines.Count);
        Assert.Equal(9500.00m, result.Total);

        var draftInvoice = result.ToDraftInvoice(
            lease.OrganizationId,
            lease.Id,
            lease.PropertyId,
            lease.UnitId,
            Guid.NewGuid(),
            new DateTime(2026, 3, 25, 10, 0, 0, DateTimeKind.Utc));

        Assert.Equal(InvoiceStatus.Draft, draftInvoice.Status);
        Assert.Equal(9500.00m, draftInvoice.Subtotal);
        Assert.Equal(9500.00m, draftInvoice.Total);
        Assert.Equal(9500.00m, draftInvoice.BalanceDue);
        Assert.Equal(3, draftInvoice.Lines.Count);
    }

    [Fact]
    public void Calculate_WhenDiscountExceedsTotalCharges_ShouldThrowNegativeInvoiceError()
    {
        var request = new BillingCalculationRequest(
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 12, 31),
            null,
            10000m,
            RentFrequency.Monthly,
            5,
            "USD",
            BillingPeriod.ForMonth(2026, 4),
            new DateOnly(2026, 4, 1),
            Discounts: [new BillingDiscountInput("Excessive promo", 12000m)]);

        var ex = Assert.Throws<InvalidOperationException>(() => _engine.Calculate(request));
        Assert.Contains("cannot be negative", ex.Message);
    }

    [Fact]
    public void Calculate_QuarterlyFullCycleVsProratedStart_ShouldMaintainExactTotals()
    {
        var fullQuarter = _engine.Calculate(new BillingCalculationRequest(
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 12, 31),
            null,
            30000m,
            RentFrequency.Quarterly,
            5,
            "USD",
            new BillingPeriod(new DateOnly(2026, 1, 1), new DateOnly(2026, 3, 31)),
            new DateOnly(2026, 1, 1)));

        Assert.False(fullQuarter.IsProrated);
        Assert.Single(fullQuarter.Lines);
        Assert.Equal(30000.00m, fullQuarter.Total);

        var proratedQuarter = _engine.Calculate(new BillingCalculationRequest(
            new DateOnly(2026, 1, 16),
            new DateOnly(2026, 12, 31),
            null,
            30000m,
            RentFrequency.Quarterly,
            5,
            "USD",
            new BillingPeriod(new DateOnly(2026, 1, 1), new DateOnly(2026, 3, 31)),
            new DateOnly(2026, 1, 16)));

        Assert.True(proratedQuarter.IsProrated);
        Assert.Equal(3, proratedQuarter.Lines.Count);
        Assert.Equal(25161.29m, proratedQuarter.Total);
    }

    [Fact]
    public void Calculate_WhenPeriodOutsideLeaseRange_ShouldThrow()
    {
        var request = new BillingCalculationRequest(
            new DateOnly(2026, 6, 1),
            new DateOnly(2027, 5, 31),
            null,
            10000m,
            RentFrequency.Monthly,
            1,
            "USD",
            BillingPeriod.ForMonth(2026, 5),
            new DateOnly(2026, 5, 1));

        Assert.Throws<InvalidOperationException>(() => _engine.Calculate(request));
    }

    [Fact]
    public void CurrencyCode_AndOrganizationDefaultCurrency_ShouldNormalizeAndValidate()
    {
        var org = Organization.Create("Cairo Estates", DateTime.UtcNow, defaultCurrency: "egp");
        Assert.Equal("EGP", org.DefaultCurrency);

        org.UpdateDefaultCurrency("sar", DateTime.UtcNow);
        Assert.Equal("SAR", org.DefaultCurrency);

        Assert.Throws<ArgumentException>(() => CurrencyCode.Normalize("US"));
        Assert.Throws<ArgumentException>(() => CurrencyCode.Normalize("123"));
    }
}
