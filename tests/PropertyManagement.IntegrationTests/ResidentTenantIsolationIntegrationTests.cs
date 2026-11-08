using Microsoft.EntityFrameworkCore;
using PropertyManagement.Application.Abstractions.Security;
using PropertyManagement.Domain.Entities.Residents;
using PropertyManagement.Infrastructure.Persistence;
using Xunit;

namespace PropertyManagement.IntegrationTests;

public class ResidentTenantIsolationIntegrationTests
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
    public async Task Query_WhenAuthenticatedAsCompanyA_ShouldOnlyReturnCompanyAResidents()
    {
        var dbName = Guid.NewGuid().ToString();
        var companyA = Guid.NewGuid();
        var companyB = Guid.NewGuid();

        // 1. Seed data using a system context (null currentUser)
        await using (var seedContext = CreateDbContext(dbName, null))
        {
            var residentA = Resident.Create(companyA, "Ali", "Hassan", "+201011111111", DateTime.UtcNow);
            var residentB = Resident.Create(companyB, "George", "Nader", "+201022222222", DateTime.UtcNow);

            await seedContext.Residents.AddRangeAsync(residentA, residentB);
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
            var results = await contextA.Residents.ToListAsync();

            Assert.Single(results);
            Assert.Equal("Ali", results[0].FirstName);
            Assert.Equal(companyA, results[0].OrganizationId);
        }
    }

    [Fact]
    public async Task Query_WhenUserHasNoOrganizationContext_ShouldFailClosedAndReturnZeroResidents()
    {
        var dbName = Guid.NewGuid().ToString();
        var companyA = Guid.NewGuid();

        await using (var seedContext = CreateDbContext(dbName, null))
        {
            var resident = Resident.Create(companyA, "Secret", "Resident", "+201099999999", DateTime.UtcNow);
            await seedContext.Residents.AddAsync(resident);
            await seedContext.SaveChangesAsync();
        }

        var userWithoutOrg = new TestCurrentUser
        {
            UserId = Guid.NewGuid(),
            OrganizationId = null,
            Role = null
        };

        await using (var unScopedContext = CreateDbContext(dbName, userWithoutOrg))
        {
            var results = await unScopedContext.Residents.ToListAsync();
            Assert.Empty(results);
        }
    }

    [Fact]
    public async Task SaveChanges_WhenAttemptingCrossTenantResidentWrite_ShouldThrowInvalidOperationException()
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

        var maliciousResident = Resident.Create(companyB, "Hacked", "Resident", "+201000000000", DateTime.UtcNow);
        await contextA.Residents.AddAsync(maliciousResident);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => contextA.SaveChangesAsync());
        Assert.Contains("Cross-tenant write operation detected", ex.Message);
    }
}
