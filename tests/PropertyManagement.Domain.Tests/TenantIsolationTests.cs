using PropertyManagement.Domain.Common;
using PropertyManagement.Domain.Entities.Leases;
using PropertyManagement.Domain.Entities.Properties;
using PropertyManagement.Domain.Entities.Residents;
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
    public void Resident_ShouldImplement_IOrganizationScoped()
    {
        var orgId = Guid.NewGuid();
        var resident = Resident.Create(
            orgId,
            "Mohamed",
            "Ali",
            "01012345678",
            DateTime.UtcNow,
            email: "m.ali@example.com",
            nationalIdOrPassport: "29901011234567");

        Assert.IsAssignableFrom<IOrganizationScoped>(resident);
        Assert.Equal(orgId, resident.OrganizationId);
    }

    [Fact]
    public void Lease_ShouldImplement_IOrganizationScoped()
    {
        var orgId = Guid.NewGuid();
        var lease = Lease.CreateDraft(
            orgId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 12, 31),
            new LeaseFinancialTerms(10000m, 20000m),
            DateTime.UtcNow);

        Assert.IsAssignableFrom<IOrganizationScoped>(lease);
        Assert.Equal(orgId, lease.OrganizationId);
    }
}
