using Microsoft.Extensions.Options;
using PropertyManagement.Infrastructure.Common.Security;
using PropertyManagement.Infrastructure.Email;
using DomainUnit = PropertyManagement.Domain.Entities.Properties.Unit;
using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using PropertyManagement.Application.Abstractions.Events;
using PropertyManagement.Application.Abstractions.Security;
using PropertyManagement.Domain.Entities.Leases;
using PropertyManagement.Domain.Entities.Leases.Events;
using PropertyManagement.Domain.Entities.Organizations;
using PropertyManagement.Domain.Entities.Properties;
using PropertyManagement.Domain.Entities.Residents;
using PropertyManagement.Domain.Enums;
using PropertyManagement.Infrastructure.Events;
using PropertyManagement.Infrastructure.Persistence;
using PropertyManagement.Infrastructure.Persistence.Repositories;

namespace PropertyManagement.IntegrationTests;

public sealed class UnifiedResidentAndSecurityHardeningIntegrationTests
{
    private sealed class TestCurrentUser : ICurrentUser
    {
        public Guid? UserId { get; set; }
        public Guid? OrganizationId { get; set; }
        public string? Role { get; set; }
        public string? Email { get; set; }
        public bool IsAuthenticated => UserId.HasValue;
    }

    private sealed class RecordingLeaseApprovedHandler(List<LeaseApprovedEvent> events)
        : INotificationHandler<DomainEventNotification<LeaseApprovedEvent>>
    {
        public Task Handle(DomainEventNotification<LeaseApprovedEvent> notification, CancellationToken cancellationToken)
        {
            events.Add(notification.DomainEvent);
            return Task.CompletedTask;
        }
    }

    private sealed class SqliteRelationalApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options,
        ICurrentUser? currentUser = null)
        : ApplicationDbContext(options, currentUser)
    {
        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            if (Database.IsSqlite())
            {
                foreach (var entityType in builder.Model.GetEntityTypes())
                {
                    var rowVersion = entityType.FindProperty("RowVersion");
                    if (rowVersion is not null)
                    {
                        rowVersion.IsConcurrencyToken = false;
                        rowVersion.ValueGenerated = ValueGenerated.Never;
                        rowVersion.SetDefaultValueSql("X''");
                    }
                }
            }
        }
    }

    [Fact]
    public async Task ResidentRepository_GetPaged_WithOnlyLeaseHoldersAndRoleFilter_ShouldDistinguishTenantsFromOccupants()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var utcNow = new DateTime(2026, 11, 14, 12, 0, 0, DateTimeKind.Utc);
        var orgId = Guid.NewGuid();
        var currentUser = new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = orgId, Role = "Owner" };

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var setupCtx = new SqliteRelationalApplicationDbContext(options, currentUser))
        {
            await setupCtx.Database.EnsureCreatedAsync();
            var org = Organization.Create("Unified Org", utcNow);
            typeof(Organization).GetProperty("Id")!.SetValue(org, orgId);
            var prop = Property.Create(orgId, "Nile View", "10 Nile St", PropertyType.Residential, utcNow);
            var unit = DomainUnit.Create(orgId, prop.Id, "5B", UnitType.Apartment, 15000m, utcNow);

            var primary = Resident.Create(orgId, "Primary", "Holder", "+201000000001", utcNow, nationalIdOrPassport: "29001011234567");
            var coTenant = Resident.Create(orgId, "Co", "Holder", "+201000000002", utcNow, nationalIdOrPassport: "29101011234567");
            var occupant = Resident.Create(orgId, "Only", "Occupant", "+201000000003", utcNow);
            var guarantor = Resident.Create(orgId, "Legal", "Guarantor", "+201000000004", utcNow, nationalIdOrPassport: "28001011234567");
            var unassigned = Resident.Create(orgId, "Prospect", "NoLease", "+201000000005", utcNow);

            var lease = Lease.CreateDraft(orgId, prop.Id, unit.Id, new DateOnly(2026, 12, 1), new DateOnly(2027, 11, 30), new LeaseFinancialTerms(15000m, 30000m), utcNow);
            lease.AddResident(primary.Id, LeaseResidentRole.PrimaryTenant, utcNow);
            lease.AddResident(coTenant.Id, LeaseResidentRole.CoTenant, utcNow);
            lease.AddResident(occupant.Id, LeaseResidentRole.Occupant, utcNow);
            lease.AddResident(guarantor.Id, LeaseResidentRole.Guarantor, utcNow);

            await setupCtx.Organizations.AddAsync(org);
            await setupCtx.Properties.AddAsync(prop);
            await setupCtx.Units.AddAsync(unit);
            await setupCtx.Residents.AddRangeAsync(primary, coTenant, occupant, guarantor, unassigned);
            await setupCtx.Leases.AddAsync(lease);
            await setupCtx.SaveChangesAsync();

            var repo = new ResidentRepository(setupCtx);

            var (leaseHolders, holderCount) = await repo.GetPagedAsync(orgId, null, null, 1, 20, onlyLeaseHolders: true);
            Assert.Equal(2, holderCount);
            Assert.Contains(leaseHolders, r => r.Id == primary.Id);
            Assert.Contains(leaseHolders, r => r.Id == coTenant.Id);

            var (occupants, occCount) = await repo.GetPagedAsync(orgId, null, null, 1, 20, leaseRole: LeaseResidentRole.Occupant);
            Assert.Equal(1, occCount);
            Assert.Equal(occupant.Id, occupants[0].Id);

            Assert.True(await repo.IsLeaseHolderAsync(primary.Id, orgId));
            Assert.False(await repo.IsLeaseHolderAsync(occupant.Id, orgId));
            Assert.False(await repo.IsLeaseHolderAsync(unassigned.Id, orgId));
        }
    }

    [Fact]
    public async Task SaveChanges_ShouldBlockCrossTenantDeletion_AndDispatchDomainEventsViaMediatR()
    {
        var dbName = Guid.NewGuid().ToString();
        var orgA = Guid.NewGuid();
        var orgB = Guid.NewGuid();
        var utcNow = new DateTime(2026, 11, 14, 12, 0, 0, DateTimeKind.Utc);
        var residentB = Resident.Create(orgB, "Target", "OrgB", "+201099998888", utcNow);

        var seedOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        await using (var seedCtx = new ApplicationDbContext(seedOptions, null))
        {
            await seedCtx.Residents.AddAsync(residentB);
            await seedCtx.SaveChangesAsync();
        }

        // Attempt cross-tenant deletion as OrgA
        var userA = new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = orgA, Role = "Owner" };
        await using (var ctxA = new ApplicationDbContext(seedOptions, userA))
        {
            ctxA.Residents.Attach(residentB);
            ctxA.Residents.Remove(residentB);
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => ctxA.SaveChangesAsync());
            Assert.Contains("Cross-tenant deletion detected", ex.Message);
        }

        // Domain event dispatch on Approve + SaveChangesAsync
        var captured = new List<LeaseApprovedEvent>();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<MediatRDomainEventDispatcher>());
        services.AddSingleton(captured);
        services.AddScoped<INotificationHandler<DomainEventNotification<LeaseApprovedEvent>>, RecordingLeaseApprovedHandler>();
        await using var sp = services.BuildServiceProvider();
        var dispatcher = new MediatRDomainEventDispatcher(sp.GetRequiredService<IPublisher>());

        await using var dispatchCtx = new ApplicationDbContext(seedOptions, userA, dispatcher);
        var lease = Lease.CreateDraft(orgA, Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 12, 1), new DateOnly(2027, 11, 30), new LeaseFinancialTerms(10000m, 20000m), utcNow);
        lease.AddResident(Guid.NewGuid(), LeaseResidentRole.PrimaryTenant, utcNow);
        lease.SubmitForApproval(utcNow);
        lease.Approve(utcNow);

        await dispatchCtx.Leases.AddAsync(lease);
        await dispatchCtx.SaveChangesAsync();

        Assert.Single(captured);
        Assert.Equal(lease.Id, captured[0].LeaseId);
    }

    [Fact]
    public void HmacVerificationCodeProtector_ShouldVerifyDeterministicallyAcrossInstances()
    {
        var emailOpts = Options.Create(new EmailConfirmationOptions { BaseUrl = "https://example.com" });
        var jwtOpts = Options.Create(new JwtOptions { Secret = "a-very-long-32-plus-char-secret-key-value", Issuer = "pm", Audience = "pm" });

        var protectorInstance1 = new HmacVerificationCodeProtector(emailOpts, jwtOpts);
        var protectorInstance2 = new HmacVerificationCodeProtector(emailOpts, jwtOpts);

        var protectedCode = protectorInstance1.Protect("482910");
        Assert.NotEqual("482910", protectedCode);
        Assert.True(protectorInstance2.Verify(protectedCode, "482910"));
        Assert.False(protectorInstance2.Verify(protectedCode, "000000"));
    }
}
