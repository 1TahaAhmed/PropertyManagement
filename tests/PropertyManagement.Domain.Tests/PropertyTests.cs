using PropertyManagement.Domain.Entities.Properties;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Domain.Tests;

public sealed class PropertyTests
{
    private readonly DateTime _now = DateTime.UtcNow;
    private const string ValidAddress = "10 El-Nasr St, Nasr City, Cairo";

    [Fact]
    public void Create_ShouldSucceed_WhenValidInputs()
    {
        // Arrange
        var tenantId = Guid.NewGuid();

        // Act
        var property = Property.Create(tenantId, "Sunrise Tower", ValidAddress, PropertyType.Residential, _now);

        // Assert
        Assert.NotEqual(Guid.Empty, property.Id);
        Assert.Equal(tenantId, property.TenantId);
        Assert.Equal("Sunrise Tower", property.Name);
        Assert.Equal(ValidAddress, property.Address);
        Assert.Equal(PropertyStatus.Active, property.Status);
    }

    [Fact]
    public void Create_ShouldThrow_WhenTenantIdIsEmpty()
    {
        Assert.Throws<ArgumentException>(() =>
            Property.Create(Guid.Empty, "Sunrise Tower", ValidAddress, PropertyType.Residential, _now));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_ShouldThrow_WhenNameIsInvalid(string? invalidName)
    {
        Assert.ThrowsAny<ArgumentException>(() =>
            Property.Create(Guid.NewGuid(), invalidName!, ValidAddress, PropertyType.Residential, _now));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_ShouldThrow_WhenAddressIsInvalid(string? invalidAddress)
    {
        Assert.ThrowsAny<ArgumentException>(() =>
            Property.Create(Guid.NewGuid(), "Sunrise Tower", invalidAddress!, PropertyType.Residential, _now));
    }

    [Fact]
    public void Archive_ShouldChangeStatusToArchived()
    {
        var property = Property.Create(Guid.NewGuid(), "Sunrise Tower", ValidAddress, PropertyType.Residential, _now);

        property.Archive(_now);

        Assert.Equal(PropertyStatus.Archived, property.Status);
    }

    [Fact]
    public void UpdateDetails_ShouldThrow_WhenPropertyIsArchived()
    {
        var property = Property.Create(Guid.NewGuid(), "Sunrise Tower", ValidAddress, PropertyType.Residential, _now);
        property.Archive(_now);

        Assert.Throws<InvalidOperationException>(() =>
            property.UpdateDetails("New Name", "New Address", "Desc", PropertyType.Commercial, _now));
    }
}