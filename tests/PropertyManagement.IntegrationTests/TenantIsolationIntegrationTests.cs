using Microsoft.EntityFrameworkCore;
using PropertyManagement.Application.Abstractions.Security;
using PropertyManagement.Domain.Entities.Properties;
using PropertyManagement.Domain.Enums;
using PropertyManagement.Infrastructure.Persistence;
using Xunit;

namespace PropertyManagement.IntegrationTests;

public class TenantIsolationIntegrationTests
{
    private class TestCurrentUser : ICurrentUser
    {
        public Guid? UserId { get; set; }
        public Guid? OrganizationId { get; set; }
        public string? Role { get; set; }
        public string? Email { get; set; }
        public bool IsAuthenticated => UserId.HasValue;
    }

    private static ApplicationDbContext CreateDbContext(string dbName, ICurrentUser? currentUser)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        return new ApplicationDbContext(options, currentUser);
    }

    [Fact]
    public async Task Query_WhenAuthenticatedAsCompanyA_ShouldOnlyReturnCompanyAProperties()
    {
        var dbName = Guid.NewGuid().ToString();
        var companyA = Guid.NewGuid();
        var companyB = Guid.NewGuid();

        // 1. Seed data using a bypassed context
        await using (var seedContext = CreateDbContext(dbName, null))
        {
            var propA = Property.Create(companyA, "Tower A", "Address A", PropertyType.Commercial, DateTime.UtcNow);
            var propB = Property.Create(companyB, "Tower B", "Address B", PropertyType.Residential, DateTime.UtcNow);

            await seedContext.Properties.AddRangeAsync(propA, propB);
            await seedContext.SaveChangesAsync();
        }

        // 2. Query as Company A user
        var userA = new TestCurrentUser
        {
            UserId = Guid.NewGuid(),
            OrganizationId = companyA,
            Role = "Owner"
        };

        await using (var contextA = CreateDbContext(dbName, userA))
        {
            var results = await contextA.Properties.ToListAsync();

            Assert.Single(results);
            Assert.Equal("Tower A", results[0].Name);
            Assert.Equal(companyA, results[0].OrganizationId);
        }
    }

    [Fact]
    public async Task Query_WhenUserHasNoOrganizationContext_ShouldFailClosedAndReturnZeroResults()
    {
        var dbName = Guid.NewGuid().ToString();
        var companyA = Guid.NewGuid();

        // 1. Seed data
        await using (var seedContext = CreateDbContext(dbName, null))
        {
            var prop = Property.Create(companyA, "Secret Tower", "Address A", PropertyType.Commercial, DateTime.UtcNow);
            await seedContext.Properties.AddAsync(prop);
            await seedContext.SaveChangesAsync();
        }

        // 2. Query as user without organization context (e.g., token without organization_id claim)
        var userWithoutOrg = new TestCurrentUser
        {
            UserId = Guid.NewGuid(),
            OrganizationId = null,
            Role = null
        };

        await using (var unScopedContext = CreateDbContext(dbName, userWithoutOrg))
        {
            var results = await unScopedContext.Properties.ToListAsync();

            // Security assertion: Must FAIL-CLOSED (return 0 rows, never leak)
            Assert.Empty(results);
        }
    }

    [Fact]
    public async Task SaveChanges_WhenAttemptingCrossTenantWrite_ShouldThrowInvalidOperationException()
    {
        var dbName = Guid.NewGuid().ToString();
        var companyA = Guid.NewGuid();
        var companyB = Guid.NewGuid();

        var userA = new TestCurrentUser
        {
            UserId = Guid.NewGuid(),
            OrganizationId = companyA,
            Role = "Owner"
        };

        await using var contextA = CreateDbContext(dbName, userA);

        // Attacker from Company A tries to create property under Company B's ID
        var maliciousProperty = Property.Create(companyB, "Hacked Tower", "Address X", PropertyType.Commercial, DateTime.UtcNow);

        await contextA.Properties.AddAsync(maliciousProperty);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => contextA.SaveChangesAsync());
        Assert.Contains("Cross-tenant write operation detected", ex.Message);
    }
}
