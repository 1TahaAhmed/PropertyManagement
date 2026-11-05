using PropertyManagement.Domain.Common;
using PropertyManagement.Domain.Entities.Properties;
using PropertyManagement.Domain.Entities.Tenants;
using PropertyManagement.Domain.Enums;
using Xunit;

namespace PropertyManagement.Domain.Tests;

public class TenantIsolationTests
{
    [Fact]
    public void Property_ShouldImplement_IOrganizationScoped()
    {
        var orgId = Guid.NewGuid();
        var property = Property.Create(
            orgId,
            "Al-Ahly Tower",
            "123 Street",
            PropertyType.Residential,
            DateTime.UtcNow);

        Assert.IsAssignableFrom<IOrganizationScoped>(property);
        Assert.Equal(orgId, property.OrganizationId);
    }

    [Fact]
    public void Unit_ShouldImplement_IOrganizationScoped()
    {
        var orgId = Guid.NewGuid();
        var unit = Unit.Create(
            orgId,
            Guid.NewGuid(),
            "A-101",
            UnitType.Apartment,
            5000m,
            DateTime.UtcNow);

        Assert.IsAssignableFrom<IOrganizationScoped>(unit);
        Assert.Equal(orgId, unit.OrganizationId);
    }

    [Fact]
    public void Tenant_ShouldImplement_IOrganizationScoped()
    {
        var orgId = Guid.NewGuid();
        var tenant = Tenant.Create(
            orgId,
            "Mohamed",
            "Ali",
            "m.ali@example.com",
            "01012345678",
            DateTime.UtcNow);

        Assert.IsAssignableFrom<IOrganizationScoped>(tenant);
        Assert.Equal(orgId, tenant.OrganizationId);
    }
}
