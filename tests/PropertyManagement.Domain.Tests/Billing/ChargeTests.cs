using PropertyManagement.Domain.Entities.Billing;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Domain.Tests.Billing;

public sealed class ChargeTests
{
    private static readonly DateTime FixedNow = new(2026, 3, 1, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Create_WhenDiscountOrCreditAdjustment_ShouldStoreNegativeSignedAmount()
    {
        var discount = Charge.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            ChargeType.Discount, "Move-in promo", 1m, 1000m, "usd", FixedNow);

        Assert.Equal("USD", discount.Currency);
        Assert.Equal(ChargeStatus.Pending, discount.Status);
        Assert.Equal(-1000.00m, discount.Amount);
        Assert.Equal(1000.00m, discount.UnitPrice);
    }

    [Fact]
    public void ChargeLifecycle_ShouldMarkBilled_ReleaseToPending_AndPreventDirectVoidWhenBilled()
    {
        var charge = Charge.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            ChargeType.Parking, "Reserved parking spot", 1m, 500m, "USD", FixedNow,
            new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31));

        var invoiceId = Guid.NewGuid();
        charge.MarkBilled(invoiceId, FixedNow.AddHours(1));

        Assert.Equal(ChargeStatus.Billed, charge.Status);
        Assert.Equal(invoiceId, charge.InvoiceId);
        Assert.Throws<InvalidOperationException>(() => charge.Void("Should fail while billed", FixedNow.AddHours(2)));

        charge.ReleaseToPending(FixedNow.AddHours(3));
        Assert.Equal(ChargeStatus.Pending, charge.Status);
        Assert.Null(charge.InvoiceId);

        charge.Void("Cancelled by manager", FixedNow.AddHours(4));
        Assert.Equal(ChargeStatus.Voided, charge.Status);
        Assert.Equal("Cancelled by manager", charge.VoidReason);
    }

    [Fact]
    public void Create_WithMismatchedPeriodBounds_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() => Charge.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            ChargeType.Utility, "Water", 1m, 200m, "USD", FixedNow,
            periodStart: new DateOnly(2026, 3, 1), periodEnd: null));

        Assert.Throws<ArgumentException>(() => Charge.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            ChargeType.Utility, "Water", 1m, 200m, "USD", FixedNow,
            periodStart: new DateOnly(2026, 3, 15), periodEnd: new DateOnly(2026, 3, 10)));
    }
}
