using PropertyManagement.Domain.Entities.Properties;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Domain.Tests;

public sealed class PropertyTests
{
    private static readonly DateTime FixedNow = new(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc);
    private const string ValidAddress = "10 El-Nasr St, Nasr City, Cairo";

    [Fact]
    public void Create_ShouldSucceed_WhenValidInputs()
    {
        var tenantId = Guid.NewGuid();

        var property = Property.Create(tenantId, "Sunrise Tower", ValidAddress, PropertyType.Residential, FixedNow);

        Assert.NotEqual(Guid.Empty, property.OrganizationId);
        Assert.Equal(tenantId, property.TenantId);
        Assert.Equal("Sunrise Tower", property.Name);
        Assert.Equal(ValidAddress, property.Address);
        Assert.Equal(PropertyStatus.Active, property.Status);
    }

    [Fact]
    public void Create_ShouldThrow_WhenTenantIdIsEmpty()
    {
        Assert.Throws<ArgumentException>(() =>
            Property.Create(Guid.Empty, "Sunrise Tower", ValidAddress, PropertyType.Residential, FixedNow));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_ShouldThrow_WhenNameIsInvalid(string? invalidName)
    {
        Assert.ThrowsAny<ArgumentException>(() =>
            Property.Create(Guid.NewGuid(), invalidName!, ValidAddress, PropertyType.Residential, FixedNow));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_ShouldThrow_WhenAddressIsInvalid(string? invalidAddress)
    {
        Assert.ThrowsAny<ArgumentException>(() =>
            Property.Create(Guid.NewGuid(), "Sunrise Tower", invalidAddress!, PropertyType.Residential, FixedNow));
    }

    [Fact]
    public void Archive_ShouldChangeStatusToArchived()
    {
        var property = Property.Create(Guid.NewGuid(), "Sunrise Tower", ValidAddress, PropertyType.Residential, FixedNow);

        property.Archive(FixedNow);

        Assert.Equal(PropertyStatus.Archived, property.Status);
    }

    [Fact]
    public void Archive_ShouldBeIdempotent_AndNotChangeUpdatedAtOnSecondCall()
    {
        var property = Property.Create(Guid.NewGuid(), "Sunrise Tower", ValidAddress, PropertyType.Residential, FixedNow);
        var firstArchiveTime = FixedNow.AddHours(1);
        property.Archive(firstArchiveTime);

        var firstUpdatedAt = property.UpdatedAt;

        property.Archive(firstArchiveTime.AddHours(2));

        Assert.Equal(firstUpdatedAt, property.UpdatedAt);
    }

    [Fact]
    public void UpdateDetails_ShouldThrow_WhenPropertyIsArchived()
    {
        var property = Property.Create(Guid.NewGuid(), "Sunrise Tower", ValidAddress, PropertyType.Residential, FixedNow);
        property.Archive(FixedNow);

        Assert.Throws<InvalidOperationException>(() =>
            property.UpdateDetails("New Name", "New Address", "Desc", PropertyType.Commercial, FixedNow));
    }
}