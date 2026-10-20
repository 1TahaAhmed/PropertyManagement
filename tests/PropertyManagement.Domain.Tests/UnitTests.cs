using PropertyManagement.Domain.Entities.Properties;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Domain.Tests;

public sealed class UnitTests
{
    private readonly DateTime _now = DateTime.UtcNow;

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
            _now,
            floorNumber: 1,
            bedrooms: 2,
            bathrooms: 1,
            squareMeters: 90);

        Assert.NotEqual(Guid.Empty, unit.Id);
        Assert.Equal("101", unit.UnitNumber);
        Assert.Equal(5000, unit.MarketRent);
        Assert.Equal(UnitOperationalStatus.Active, unit.Status);
    }

    [Fact]
    public void Create_ShouldThrow_WhenMarketRentIsNegative()
    {
        Assert.Throws<ArgumentException>(() =>
            Unit.Create(Guid.NewGuid(), Guid.NewGuid(), "101", UnitType.Apartment, marketRent: -100, _now));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_ShouldThrow_WhenUnitNumberIsInvalid(string? invalidNumber)
    {
        Assert.ThrowsAny<ArgumentException>(() =>
            Unit.Create(Guid.NewGuid(), Guid.NewGuid(), invalidNumber!, UnitType.Apartment, 5000, _now));
    }

    [Fact]
    public void SetStatus_ShouldUpdateStatusAndTimestamp()
    {
        var unit = Unit.Create(Guid.NewGuid(), Guid.NewGuid(), "101", UnitType.Apartment, 5000, _now);

        var updateTime = _now.AddHours(1);
        unit.SetStatus(UnitOperationalStatus.UnderMaintenance, updateTime);

        Assert.Equal(UnitOperationalStatus.UnderMaintenance, unit.Status);
        Assert.Equal(updateTime, unit.UpdatedAt);
    }
}