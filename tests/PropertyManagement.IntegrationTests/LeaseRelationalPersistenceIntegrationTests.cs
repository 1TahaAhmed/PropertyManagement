using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;
using PropertyManagement.Application.Abstractions;
using PropertyManagement.Application.Abstractions.Security;
using PropertyManagement.Application.Features.Leases.AddResident;
using PropertyManagement.Application.Features.Leases.Approve;
using PropertyManagement.Application.Features.Leases.Cancel;
using PropertyManagement.Application.Features.Leases.Submit;
using PropertyManagement.Application.Features.Leases.Terminate;
using PropertyManagement.Domain.Entities.Leases;
using PropertyManagement.Domain.Entities.Organizations;
using PropertyManagement.Domain.Entities.Properties;
using PropertyManagement.Domain.Entities.Residents;
using PropertyManagement.Domain.Enums;
using PropertyManagement.Infrastructure.Persistence;
using PropertyManagement.Infrastructure.Persistence.Repositories;
using Xunit;

namespace PropertyManagement.IntegrationTests;

public class LeaseRelationalPersistenceIntegrationTests
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

    private sealed class SqlCaptureInterceptor : DbCommandInterceptor
    {
        public List<string> Commands { get; } = [];

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            Commands.Add(command.CommandText);
            return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
        }

        public override InterceptionResult<int> NonQueryExecuting(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<int> result)
        {
            Commands.Add(command.CommandText);
            return base.NonQueryExecuting(command, eventData, result);
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Commands.Add(command.CommandText);
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }

        public override InterceptionResult<DbDataReader> ReaderExecuting(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result)
        {
            Commands.Add(command.CommandText);
            return base.ReaderExecuting(command, eventData, result);
        }
    }

    /// <summary>
    /// Uses the production ApplicationDbContext configurations and model on a real relational SQLite engine.
    /// Only adapts SQL Server's native rowversion column generation (unavailable in SQLite) while leaving
    /// all Id primary key ValueGeneratedNever configurations, foreign keys, and indexes untouched.
    /// </summary>
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

    private static SqliteRelationalApplicationDbContext CreateSqliteDbContext(
        SqliteConnection connection,
        ICurrentUser? currentUser,
        SqlCaptureInterceptor? sqlInterceptor = null)
    {
        var builder = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection);

        if (sqlInterceptor is not null)
        {
            builder.AddInterceptors(sqlInterceptor);
        }

        return new SqliteRelationalApplicationDbContext(builder.Options, currentUser);
    }

    private static async Task<(Guid OrgId, Guid LeaseId, Guid PrimaryResidentId, Guid CoTenantResidentId)> SeedRelationalGraphAsync(
        SqliteConnection connection,
        TestCurrentUser currentUser,
        DateTime utcNow)
    {
        var org = Organization.Create("Nile Properties", utcNow);
        currentUser.OrganizationId = org.Id;

        var property = Property.Create(org.Id, "Zamalek Tower", "15 Brazil St", PropertyType.Residential, utcNow);
        var unit = Unit.Create(org.Id, property.Id, "12B", UnitType.Apartment, 20000m, utcNow, floorNumber: 12, bedrooms: 3, bathrooms: 2, squareMeters: 180m);
        var primaryResident = Resident.Create(org.Id, "Omar", "Khaled", "+201012345678", utcNow);
        var coTenantResident = Resident.Create(org.Id, "Nour", "Hossam", "+201087654321", utcNow);

        var lease = Lease.CreateDraft(
            organizationId: org.Id,
            propertyId: property.Id,
            unitId: unit.Id,
            startDate: new DateOnly(2026, 12, 1),
            endDate: new DateOnly(2027, 11, 30),
            terms: new LeaseFinancialTerms(20000m, 40000m),
            utcNow: utcNow);
        lease.AddResident(primaryResident.Id, LeaseResidentRole.PrimaryTenant, utcNow);

        await using var seedContext = CreateSqliteDbContext(connection, currentUser);
        await seedContext.Database.EnsureCreatedAsync();
        await seedContext.Organizations.AddAsync(org);
        await seedContext.Properties.AddAsync(property);
        await seedContext.Units.AddAsync(unit);
        await seedContext.Residents.AddRangeAsync(primaryResident, coTenantResident);
        await seedContext.Leases.AddAsync(lease);
        await seedContext.SaveChangesAsync();

        return (org.Id, lease.Id, primaryResident.Id, coTenantResident.Id);
    }

    [Fact]
    public async Task AddResidentToLease_OnRealRelationalDatabase_ShouldIssueInsertAndPersistWithoutConcurrencyConflict()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var utcNow = new DateTime(2026, 11, 14, 10, 0, 0, DateTimeKind.Utc);
        var currentUser = new TestCurrentUser
        {
            UserId = Guid.NewGuid(),
            Role = "Owner"
        };

        var (orgId, leaseId, primaryId, coTenantId) = await SeedRelationalGraphAsync(connection, currentUser, utcNow);

        var sqlInterceptor = new SqlCaptureInterceptor();
        await using (var commandContext = CreateSqliteDbContext(connection, currentUser, sqlInterceptor))
        {
            var handler = new AddResidentToLeaseCommandHandler(
                new LeaseRepository(commandContext),
                new ResidentRepository(commandContext),
                commandContext,
                new TestClock(utcNow.AddMinutes(5)));

            var result = await handler.Handle(
                new AddResidentToLeaseCommand(orgId, leaseId, coTenantId, LeaseResidentRole.CoTenant),
                CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Value);
            Assert.Equal(2, result.Value!.Residents.Count);
        }

        // Must execute INSERT on LeaseResidents — never UPDATE on a non-existent LeaseResident row
        Assert.Contains(sqlInterceptor.Commands, sql => sql.Contains("INSERT INTO \"LeaseResidents\"", StringComparison.Ordinal));
        Assert.DoesNotContain(sqlInterceptor.Commands, sql => sql.Contains("UPDATE \"LeaseResidents\"", StringComparison.Ordinal));

        // Lease UPDATE must only touch UpdatedAtUtc (not all columns via Tracked Update)
        var leaseUpdates = sqlInterceptor.Commands
            .Where(sql => sql.Contains("UPDATE \"Leases\"", StringComparison.Ordinal))
            .ToList();
        var leaseUpdateSql = Assert.Single(leaseUpdates);
        Assert.Contains("\"UpdatedAtUtc\"", leaseUpdateSql, StringComparison.Ordinal);
        Assert.DoesNotContain("\"RentAmount\"", leaseUpdateSql, StringComparison.Ordinal);
        Assert.DoesNotContain("\"SecurityDeposit\"", leaseUpdateSql, StringComparison.Ordinal);
        Assert.DoesNotContain("\"StartDate\"", leaseUpdateSql, StringComparison.Ordinal);

        await using (var verifyContext = CreateSqliteDbContext(connection, currentUser))
        {
            var persistedLease = await new LeaseRepository(verifyContext).GetByIdAsync(leaseId, orgId);
            Assert.NotNull(persistedLease);
            Assert.Equal(2, persistedLease!.Residents.Count);
            Assert.Contains(persistedLease.Residents, r => r.ResidentId == primaryId && r.Role == LeaseResidentRole.PrimaryTenant);
            Assert.Contains(persistedLease.Residents, r => r.ResidentId == coTenantId && r.Role == LeaseResidentRole.CoTenant);
        }
    }

    [Fact]
    public async Task LeaseLifecycleHandlers_WithoutRedundantRepositoryUpdate_ShouldUpdateOnlyChangedColumnsOnRelationalDatabase()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var utcNow = new DateTime(2026, 11, 14, 10, 0, 0, DateTimeKind.Utc);
        var currentUser = new TestCurrentUser
        {
            UserId = Guid.NewGuid(),
            Role = "Owner"
        };

        var (orgId, leaseId, _, _) = await SeedRelationalGraphAsync(connection, currentUser, utcNow);

        // 1. Submit for approval
        var submitSql = new SqlCaptureInterceptor();
        await using (var submitContext = CreateSqliteDbContext(connection, currentUser, submitSql))
        {
            var handler = new SubmitLeaseForApprovalCommandHandler(
                new LeaseRepository(submitContext),
                submitContext,
                new TestClock(utcNow.AddMinutes(1)));

            var submitResult = await handler.Handle(new SubmitLeaseForApprovalCommand(orgId, leaseId), CancellationToken.None);
            Assert.True(submitResult.IsSuccess);
            Assert.Equal(LeaseStatus.PendingApproval, submitResult.Value!.Status);
        }

        var submitUpdateSql = Assert.Single(submitSql.Commands, s => s.Contains("UPDATE \"Leases\"", StringComparison.Ordinal));
        Assert.Contains("\"Status\"", submitUpdateSql, StringComparison.Ordinal);
        Assert.Contains("\"UpdatedAtUtc\"", submitUpdateSql, StringComparison.Ordinal);
        Assert.DoesNotContain("\"RentAmount\"", submitUpdateSql, StringComparison.Ordinal);
        Assert.DoesNotContain("\"PropertyId\"", submitUpdateSql, StringComparison.Ordinal);

        // 2. Approve
        var approveSql = new SqlCaptureInterceptor();
        await using (var approveContext = CreateSqliteDbContext(connection, currentUser, approveSql))
        {
            var handler = new ApproveLeaseCommandHandler(
                new LeaseRepository(approveContext),
                approveContext,
                new TestClock(utcNow.AddMinutes(2)));

            var approveResult = await handler.Handle(new ApproveLeaseCommand(orgId, leaseId), CancellationToken.None);
            Assert.True(approveResult.IsSuccess);
            Assert.Equal(LeaseStatus.Active, approveResult.Value!.Status);
        }

        var approveUpdateSql = Assert.Single(approveSql.Commands, s => s.Contains("UPDATE \"Leases\"", StringComparison.Ordinal));
        Assert.Contains("\"Status\"", approveUpdateSql, StringComparison.Ordinal);
        Assert.Contains("\"UpdatedAtUtc\"", approveUpdateSql, StringComparison.Ordinal);
        Assert.DoesNotContain("\"RentAmount\"", approveUpdateSql, StringComparison.Ordinal);
        Assert.DoesNotContain("\"StartDate\"", approveUpdateSql, StringComparison.Ordinal);

        // 3. Terminate
        var terminateSql = new SqlCaptureInterceptor();
        await using (var terminateContext = CreateSqliteDbContext(connection, currentUser, terminateSql))
        {
            var handler = new TerminateLeaseCommandHandler(
                new LeaseRepository(terminateContext),
                terminateContext,
                new TestClock(utcNow.AddMinutes(3)));

            var terminateResult = await handler.Handle(
                new TerminateLeaseCommand(orgId, leaseId, new DateOnly(2027, 3, 1), "Mutual agreement", currentUser.UserId!.Value),
                CancellationToken.None);

            Assert.True(terminateResult.IsSuccess);
            Assert.Equal(LeaseStatus.Terminated, terminateResult.Value!.Status);
        }

        var terminateUpdateSql = Assert.Single(terminateSql.Commands, s => s.Contains("UPDATE \"Leases\"", StringComparison.Ordinal));
        Assert.Contains("\"Status\"", terminateUpdateSql, StringComparison.Ordinal);
        Assert.Contains("\"TerminationDate\"", terminateUpdateSql, StringComparison.Ordinal);
        Assert.DoesNotContain("\"RentAmount\"", terminateUpdateSql, StringComparison.Ordinal);

        // 4. Cancel a separate Draft lease
        Guid propertyId2;
        Guid unitId2;
        await using (var readCtx = CreateSqliteDbContext(connection, currentUser))
        {
            propertyId2 = (await readCtx.Properties.FirstAsync()).Id;
            unitId2 = (await readCtx.Units.FirstAsync()).Id;
        }

        var draftLease2 = Lease.CreateDraft(
            orgId,
            propertyId2,
            unitId2,
            new DateOnly(2028, 1, 1),
            new DateOnly(2028, 12, 31),
            new LeaseFinancialTerms(18000m, 36000m),
            utcNow);

        await using (var seedDraft2 = CreateSqliteDbContext(connection, currentUser))
        {
            await seedDraft2.Leases.AddAsync(draftLease2);
            await seedDraft2.SaveChangesAsync();
        }

        var cancelSql = new SqlCaptureInterceptor();
        await using (var cancelContext = CreateSqliteDbContext(connection, currentUser, cancelSql))
        {
            var handler = new CancelLeaseCommandHandler(
                new LeaseRepository(cancelContext),
                cancelContext,
                new TestClock(utcNow.AddMinutes(4)));

            var cancelResult = await handler.Handle(new CancelLeaseCommand(orgId, draftLease2.Id), CancellationToken.None);
            Assert.True(cancelResult.IsSuccess);
            Assert.Equal(LeaseStatus.Cancelled, cancelResult.Value!.Status);
        }

        var cancelUpdateSql = Assert.Single(cancelSql.Commands, s => s.Contains("UPDATE \"Leases\"", StringComparison.Ordinal));
        Assert.Contains("\"Status\"", cancelUpdateSql, StringComparison.Ordinal);
        Assert.Contains("\"UpdatedAtUtc\"", cancelUpdateSql, StringComparison.Ordinal);
        Assert.DoesNotContain("\"RentAmount\"", cancelUpdateSql, StringComparison.Ordinal);
    }
}
