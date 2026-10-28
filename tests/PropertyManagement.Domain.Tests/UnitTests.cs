using PropertyManagement.Domain.Entities.Properties;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Domain.Tests;

public sealed class UnitTests
{
    private static readonly DateTime FixedNow = new(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Create_ShouldSucceed_WhenValidInputs()
    {
        var tenantId = Guid.NewGuid();
        var propertyId = Guid.NewGuid();

        var unit = Unit.Create(
            tenantId,
            propertyId,
            "101",
            UnitType.Apartment,
            marketRent: 5000,
            FixedNow,
            floorNumber: 1,
            bedrooms: 2,
            bathrooms: 1,
            squareMeters: 90);

        Assert.NotEqual(Guid.Empty, unit.OrganizationId);
        Assert.Equal("101", unit.UnitNumber);
        Assert.Equal(5000, unit.MarketRent);
        Assert.Equal(UnitOperationalStatus.Active, unit.Status);
    }

    [Fact]
    public void Create_ShouldTrimUnitNumber()
    {
        var unit = Unit.Create(Guid.NewGuid(), Guid.NewGuid(), "  101  ", UnitType.Apartment, 5000, FixedNow);

        Assert.Equal("101", unit.UnitNumber);
    }

    [Fact]
    public void Create_ShouldThrow_WhenMarketRentIsNegative()
    {
        Assert.Throws<ArgumentException>(() =>
            Unit.Create(Guid.NewGuid(), Guid.NewGuid(), "101", UnitType.Apartment, marketRent: -100, FixedNow));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_ShouldThrow_WhenUnitNumberIsInvalid(string? invalidNumber)
    {
        Assert.ThrowsAny<ArgumentException>(() =>
            Unit.Create(Guid.NewGuid(), Guid.NewGuid(), invalidNumber!, UnitType.Apartment, 5000, FixedNow));
    }

    [Fact]
    public void UpdateDetails_ShouldThrow_WhenBedroomsIsNegative()
    {
        var unit = Unit.Create(Guid.NewGuid(), Guid.NewGuid(), "101", UnitType.Apartment, 5000, FixedNow);

        Assert.Throws<ArgumentException>(() =>
            unit.UpdateDetails("101", UnitType.Apartment, 5000, null, bedrooms: -1, bathrooms: 1, squareMeters: 50, "", FixedNow));
    }

    [Fact]
    public void SetStatus_ShouldThrow_WhenStatusIsUndefined()
    {
        var unit = Unit.Create(Guid.NewGuid(), Guid.NewGuid(), "101", UnitType.Apartment, 5000, FixedNow);

        Assert.Throws<ArgumentException>(() =>
            unit.SetStatus((UnitOperationalStatus)99, FixedNow));
    }

    [Fact]
    public void SetStatus_ShouldUpdateStatusAndTimestamp()
    {
        var unit = Unit.Create(Guid.NewGuid(), Guid.NewGuid(), "101", UnitType.Apartment, 5000, FixedNow);

        var updateTime = FixedNow.AddHours(1);
        unit.SetStatus(UnitOperationalStatus.UnderMaintenance, updateTime);

        Assert.Equal(UnitOperationalStatus.UnderMaintenance, unit.Status);
        Assert.Equal(updateTime, unit.UpdatedAt);
    }
}