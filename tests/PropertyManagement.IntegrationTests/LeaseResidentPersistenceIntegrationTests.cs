using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using PropertyManagement.Application.Abstractions;
using PropertyManagement.Application.Abstractions.Security;
using PropertyManagement.Application.Features.Leases.AddResident;
using PropertyManagement.Domain.Entities.Leases;
using PropertyManagement.Domain.Entities.Residents;
using PropertyManagement.Domain.Enums;
using PropertyManagement.Infrastructure.Persistence;
using PropertyManagement.Infrastructure.Persistence.Repositories;
using Xunit;

namespace PropertyManagement.IntegrationTests;

public class LeaseResidentPersistenceIntegrationTests
{
    private sealed class TestCurrentUser : ICurrentUser
    {
        public Guid? UserId { get; set; }
        public Guid? OrganizationId { get; set; }
        public string? Role { get; set; }
        public string? Email { get; set; }
        public bool IsAuthenticated => UserId.HasValue;
    }

    private sealed class TestClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; } = utcNow;
    }

    private sealed class SpyUnitOfWork(ApplicationDbContext context) : IUnitOfWork
    {
        public List<(Guid ResidentId, EntityState State)> CapturedLeaseResidentStatesBeforeSave { get; } = [];

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            context.ChangeTracker.DetectChanges();
            CapturedLeaseResidentStatesBeforeSave.AddRange(
                context.ChangeTracker
                    .Entries<LeaseResident>()
                    .Select(e => (e.Entity.ResidentId, e.State)));

            return context.SaveChangesAsync(cancellationToken);
        }

        public Task<IDbTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
            => context.BeginTransactionAsync(cancellationToken);
    }

    private static ApplicationDbContext CreateDbContext(string dbName, ICurrentUser? currentUser)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        return new ApplicationDbContext(options, currentUser);
    }

    [Fact]
    public void DomainEntities_IncludingLeaseAndLeaseResident_ShouldConfigurePrimaryKeysAsValueGeneratedNever()
    {
        using var context = CreateDbContext(Guid.NewGuid().ToString(), null);

        var leaseEntityType = context.Model.FindEntityType(typeof(Lease));
        var leaseResidentEntityType = context.Model.FindEntityType(typeof(LeaseResident));

        Assert.NotNull(leaseEntityType);
        Assert.NotNull(leaseResidentEntityType);

        var leasePk = leaseEntityType!.FindPrimaryKey()!.Properties.Single();
        var leaseResidentPk = leaseResidentEntityType!.FindPrimaryKey()!.Properties.Single();

        Assert.Equal(ValueGenerated.Never, leasePk.ValueGenerated);
        Assert.Equal(ValueGenerated.Never, leaseResidentPk.ValueGenerated);

        var domainEntityTypes = context.Model.GetEntityTypes()
            .Where(et => !et.IsOwned() &&
                         et.ClrType.Namespace is not null &&
                         et.ClrType.Namespace.StartsWith("PropertyManagement.Domain.Entities", StringComparison.Ordinal));

        foreach (var entityType in domainEntityTypes)
        {
            var pk = entityType.FindPrimaryKey();
            Assert.NotNull(pk);
            foreach (var pkProperty in pk!.Properties)
            {
                Assert.Equal(ValueGenerated.Never, pkProperty.ValueGenerated);
            }
        }
    }

    [Fact]
    public async Task AddResidentToLeaseCommandHandler_ShouldTrackNewLeaseResidentAsAddedAndPersistCleanly()
    {
        var dbName = Guid.NewGuid().ToString();
        var orgId = Guid.NewGuid();
        var utcNow = new DateTime(2026, 11, 13, 12, 0, 0, DateTimeKind.Utc);

        var currentUser = new TestCurrentUser
        {
            UserId = Guid.NewGuid(),
            OrganizationId = orgId,
            Role = "Owner"
        };

        var primaryResident = Resident.Create(orgId, "Ahmed", "Samir", "+201000000001", utcNow);
        var coTenantResident = Resident.Create(orgId, "Mona", "Adel", "+201000000002", utcNow);
        var lease = Lease.CreateDraft(
            organizationId: orgId,
            propertyId: Guid.NewGuid(),
            unitId: Guid.NewGuid(),
            startDate: new DateOnly(2026, 12, 1),
            endDate: new DateOnly(2027, 11, 30),
            terms: new LeaseFinancialTerms(15000m, 30000m),
            utcNow: utcNow);
        lease.AddResident(primaryResident.Id, LeaseResidentRole.PrimaryTenant, utcNow);

        await using (var seedContext = CreateDbContext(dbName, currentUser))
        {
            await seedContext.Residents.AddRangeAsync(primaryResident, coTenantResident);
            await seedContext.Leases.AddAsync(lease);
            await seedContext.SaveChangesAsync();
        }

        await using (var commandContext = CreateDbContext(dbName, currentUser))
        {
            var leaseRepository = new LeaseRepository(commandContext);
            var residentRepository = new ResidentRepository(commandContext);
            var spyUnitOfWork = new SpyUnitOfWork(commandContext);
            var handler = new AddResidentToLeaseCommandHandler(
                leaseRepository,
                residentRepository,
                spyUnitOfWork,
                new TestClock(utcNow.AddMinutes(5)));

            var result = await handler.Handle(
                new AddResidentToLeaseCommand(orgId, lease.Id, coTenantResident.Id, LeaseResidentRole.CoTenant),
                CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Value);
            Assert.Equal(2, result.Value!.Residents.Count);

            var existingState = Assert.Single(
                spyUnitOfWork.CapturedLeaseResidentStatesBeforeSave,
                x => x.ResidentId == primaryResident.Id);
            var addedState = Assert.Single(
                spyUnitOfWork.CapturedLeaseResidentStatesBeforeSave,
                x => x.ResidentId == coTenantResident.Id);

            Assert.Equal(EntityState.Unchanged, existingState.State);
            Assert.Equal(EntityState.Added, addedState.State);
        }

        await using (var verifyContext = CreateDbContext(dbName, currentUser))
        {
            var persistedLease = await new LeaseRepository(verifyContext).GetByIdAsync(lease.Id, orgId);
            Assert.NotNull(persistedLease);
            Assert.Equal(2, persistedLease!.Residents.Count);
            Assert.Contains(persistedLease.Residents, r => r.ResidentId == primaryResident.Id && r.Role == LeaseResidentRole.PrimaryTenant);
            Assert.Contains(persistedLease.Residents, r => r.ResidentId == coTenantResident.Id && r.Role == LeaseResidentRole.CoTenant);
        }
    }

    [Fact]
    public async Task LeaseRepositoryUpdate_WhenCalledOnTrackedLeaseWithNewResident_ShouldKeepNewResidentAsAdded()
    {
        var dbName = Guid.NewGuid().ToString();
        var orgId = Guid.NewGuid();
        var utcNow = new DateTime(2026, 11, 13, 12, 0, 0, DateTimeKind.Utc);

        var currentUser = new TestCurrentUser
        {
            UserId = Guid.NewGuid(),
            OrganizationId = orgId,
            Role = "Owner"
        };

        var primaryResident = Resident.Create(orgId, "Kareem", "Nabil", "+201000000003", utcNow);
        var occupantResident = Resident.Create(orgId, "Salma", "Tarek", "+201000000004", utcNow);
        var lease = Lease.CreateDraft(
            organizationId: orgId,
            propertyId: Guid.NewGuid(),
            unitId: Guid.NewGuid(),
            startDate: new DateOnly(2026, 12, 1),
            endDate: new DateOnly(2027, 11, 30),
            terms: new LeaseFinancialTerms(12000m, 24000m),
            utcNow: utcNow);
        lease.AddResident(primaryResident.Id, LeaseResidentRole.PrimaryTenant, utcNow);

        await using (var seedContext = CreateDbContext(dbName, currentUser))
        {
            await seedContext.Residents.AddRangeAsync(primaryResident, occupantResident);
            await seedContext.Leases.AddAsync(lease);
            await seedContext.SaveChangesAsync();
        }

        await using (var updateContext = CreateDbContext(dbName, currentUser))
        {
            var leaseRepository = new LeaseRepository(updateContext);
            var trackedLease = await leaseRepository.GetByIdAsync(lease.Id, orgId);
            Assert.NotNull(trackedLease);

            var addedLeaseResident = trackedLease!.AddResident(
                occupantResident.Id,
                LeaseResidentRole.Occupant,
                utcNow.AddMinutes(10));

            leaseRepository.Update(trackedLease);
            updateContext.ChangeTracker.DetectChanges();

            var newResidentEntry = updateContext.Entry(addedLeaseResident);
            Assert.Equal(EntityState.Added, newResidentEntry.State);

            await updateContext.SaveChangesAsync();
        }

        await using (var verifyContext = CreateDbContext(dbName, currentUser))
        {
            var persistedLease = await new LeaseRepository(verifyContext).GetByIdAsync(lease.Id, orgId);
            Assert.NotNull(persistedLease);
            Assert.Equal(2, persistedLease!.Residents.Count);
            Assert.Contains(persistedLease.Residents, r => r.ResidentId == occupantResident.Id && r.Role == LeaseResidentRole.Occupant);
        }
    }
}
