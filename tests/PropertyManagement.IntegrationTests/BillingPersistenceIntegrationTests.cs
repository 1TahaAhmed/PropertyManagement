using PropertyManagement.Application.Abstractions.Billing;
using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using PropertyManagement.Application.Abstractions.Events;
using PropertyManagement.Application.Abstractions.Security;
using PropertyManagement.Domain.Entities.Billing;
using PropertyManagement.Domain.Entities.Billing.Events;
using PropertyManagement.Domain.Entities.Billing.Services;
using PropertyManagement.Domain.Entities.Leases;
using PropertyManagement.Domain.Entities.Organizations;
using PropertyManagement.Domain.Entities.Properties;
using PropertyManagement.Domain.Entities.Residents;
using PropertyManagement.Domain.Enums;
using PropertyManagement.Infrastructure.Events;
using PropertyManagement.Infrastructure.Persistence;
using PropertyManagement.Infrastructure.Persistence.Repositories.Billing;
using DomainUnit = PropertyManagement.Domain.Entities.Properties.Unit;

namespace PropertyManagement.IntegrationTests;

public sealed class BillingPersistenceIntegrationTests
{
    private sealed class TestCurrentUser : ICurrentUser
    {
        public Guid? UserId { get; set; }
        public Guid? OrganizationId { get; set; }
        public string? Role { get; set; }
        public string? Email { get; set; }
        public bool IsAuthenticated => UserId.HasValue;
    }

    private sealed class RecordingInvoiceIssuedHandler(List<InvoiceIssuedEvent> events)
        : INotificationHandler<DomainEventNotification<InvoiceIssuedEvent>>
    {
        public Task Handle(DomainEventNotification<InvoiceIssuedEvent> notification, CancellationToken cancellationToken)
        {
            events.Add(notification.DomainEvent);
            return Task.CompletedTask;
        }
    }

    private sealed class SqliteRelationalApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options,
        ICurrentUser? currentUser = null,
        IDomainEventDispatcher? domainEventDispatcher = null)
        : ApplicationDbContext(options, currentUser, domainEventDispatcher)
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

    private static async Task<(Guid OrgId, Guid PropertyId, Guid UnitId, Guid ResidentId, Guid LeaseId)> SeedLeaseGraphAsync(
        SqliteConnection connection,
        TestCurrentUser currentUser,
        DateTime utcNow)
    {
        var org = Organization.Create("Nile Billing Estates", utcNow, defaultCurrency: "EGP");
        currentUser.OrganizationId = org.Id;

        var property = Property.Create(org.Id, "Maadi Heights", "20 Road 9", PropertyType.Residential, utcNow);
        var unit = DomainUnit.Create(org.Id, property.Id, "A-202", UnitType.Apartment, 12000m, utcNow);
        var resident = Resident.Create(org.Id, "Tariq", "Nabil", "+201001234567", utcNow, nationalIdOrPassport: "29201011234567");

        var lease = Lease.CreateDraft(
            org.Id,
            property.Id,
            unit.Id,
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 12, 31),
            new LeaseFinancialTerms(12000m, 24000m, RentFrequency.Monthly, dueDayOfMonth: 5, currency: "EGP"),
            utcNow);
        lease.AddResident(resident.Id, LeaseResidentRole.PrimaryTenant, utcNow);
        lease.SubmitForApproval(utcNow);
        lease.Approve(utcNow);

        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        await using var ctx = new SqliteRelationalApplicationDbContext(options, currentUser);
        await ctx.Database.EnsureCreatedAsync();

        await ctx.Organizations.AddAsync(org);
        await ctx.Properties.AddAsync(property);
        await ctx.Units.AddAsync(unit);
        await ctx.Residents.AddAsync(resident);
        await ctx.Leases.AddAsync(lease);
        await ctx.SaveChangesAsync();

        return (org.Id, property.Id, unit.Id, resident.Id, lease.Id);
    }

    [Fact]
    public async Task InvoicePersistence_WithChargesAdjustmentsAndDomainEvents_ShouldRoundtripCleanly()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var utcNow = new DateTime(2026, 3, 1, 10, 0, 0, DateTimeKind.Utc);
        var actorId = Guid.NewGuid();
        var currentUser = new TestCurrentUser { UserId = actorId, Role = "Owner" };
        var (orgId, propertyId, unitId, residentId, leaseId) = await SeedLeaseGraphAsync(connection, currentUser, utcNow);

        var capturedIssued = new List<InvoiceIssuedEvent>();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<MediatRDomainEventDispatcher>());
        services.AddSingleton(capturedIssued);
        services.AddScoped<INotificationHandler<DomainEventNotification<InvoiceIssuedEvent>>, RecordingInvoiceIssuedHandler>();
        await using var sp = services.BuildServiceProvider();
        var dispatcher = new MediatRDomainEventDispatcher(sp.GetRequiredService<IPublisher>());

        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;

        Guid invoiceId;
        Guid chargeId;
        await using (var writeCtx = new SqliteRelationalApplicationDbContext(options, currentUser, dispatcher))
        {
            var chargeRepo = new ChargeRepository(writeCtx);
            var invoiceRepo = new InvoiceRepository(writeCtx);
            var numberGenerator = new InvoiceNumberGenerator(writeCtx);

            var parkingCharge = Charge.Create(
                orgId, leaseId, propertyId, unitId, residentId,
                ChargeType.Parking, "March Garage Spot", 1m, 500m, "EGP", utcNow,
                new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31));
            chargeId = parkingCharge.Id;
            await chargeRepo.AddAsync(parkingCharge);

            var engine = new BillingCalculationEngine();
            var calc = engine.Calculate(new BillingCalculationRequest(
                new DateOnly(2026, 1, 1),
                new DateOnly(2026, 12, 31),
                null,
                12000m,
                RentFrequency.Monthly,
                5,
                "EGP",
                BillingPeriod.ForMonth(2026, 3),
                new DateOnly(2026, 3, 1),
                AdditionalCharges:
                [
                    new BillingAdditionalChargeInput(parkingCharge.Type, parkingCharge.Description, parkingCharge.Quantity, parkingCharge.UnitPrice, parkingCharge.Id, parkingCharge.PeriodStart, parkingCharge.PeriodEnd)
                ]));

            var idempotencyKey = $"RENT:{leaseId:N}:20260301:20260331";
            var invoice = calc.ToDraftInvoice(orgId, leaseId, propertyId, unitId, residentId, utcNow, idempotencyKey: idempotencyKey);
            invoiceId = invoice.Id;

            parkingCharge.MarkBilled(invoice.Id, utcNow);
            var invoiceNumber = await numberGenerator.GenerateNextAsync(orgId, 2026);
            invoice.Issue(invoiceNumber, new DateOnly(2026, 3, 1), utcNow);

            await invoiceRepo.AddAsync(invoice);
            await writeCtx.SaveChangesAsync();
        }

        Assert.Single(capturedIssued);
        Assert.Equal("INV-2026-000001", capturedIssued[0].InvoiceNumber);
        Assert.Equal(12500.00m, capturedIssued[0].Total);

        // Apply post-issue late fee adjustment in separate DbContext
        await using (var adjustCtx = new SqliteRelationalApplicationDbContext(options, currentUser))
        {
            var invoiceRepo = new InvoiceRepository(adjustCtx);
            var invoice = await invoiceRepo.GetByIdAsync(invoiceId, orgId);
            Assert.NotNull(invoice);
            invoice!.ApplyAdjustment(
                InvoiceAdjustmentType.Debit,
                ChargeType.LateFee,
                600m,
                "5% Late Fee",
                "Past due date",
                actorId,
                utcNow.AddDays(10));
            await adjustCtx.SaveChangesAsync();
        }

        // Verify full persisted state
        await using (var readCtx = new SqliteRelationalApplicationDbContext(options, currentUser))
        {
            var invoiceRepo = new InvoiceRepository(readCtx);
            var chargeRepo = new ChargeRepository(readCtx);

            var persisted = await invoiceRepo.GetByInvoiceNumberAsync("INV-2026-000001", orgId);
            Assert.NotNull(persisted);
            Assert.Equal(12500.00m, persisted!.Subtotal);
            Assert.Equal(600.00m, persisted.TotalAdjustments);
            Assert.Equal(13100.00m, persisted.Total);
            Assert.Equal(13100.00m, persisted.BalanceDue);
            Assert.Equal(2, persisted.Lines.Count);
            Assert.Single(persisted.Adjustments);

            var charge = await chargeRepo.GetByIdAsync(chargeId, orgId);
            Assert.NotNull(charge);
            Assert.Equal(ChargeStatus.Billed, charge!.Status);
            Assert.Equal(invoiceId, charge.InvoiceId);
        }
    }

    [Fact]
    public async Task IdempotencyConstraint_ShouldPreventDuplicateActiveRentInvoice_AndAllowReplacementAfterVoid()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var utcNow = new DateTime(2026, 3, 1, 10, 0, 0, DateTimeKind.Utc);
        var currentUser = new TestCurrentUser { UserId = Guid.NewGuid(), Role = "Owner" };
        var (orgId, propertyId, unitId, residentId, leaseId) = await SeedLeaseGraphAsync(connection, currentUser, utcNow);

        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        var key = $"RENT:{leaseId:N}:20260301:20260331";

        Guid firstId;
        await using (var ctx1 = new SqliteRelationalApplicationDbContext(options, currentUser))
        {
            var first = Invoice.CreateDraft(
                orgId, leaseId, propertyId, unitId, residentId,
                BillingPeriod.ForMonth(2026, 3), new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 5),
                "EGP", utcNow, idempotencyKey: key);
            first.AddLine(ChargeType.Rent, "March Rent", 1m, 12000m, utcNow);
            firstId = first.Id;
            await ctx1.Invoices.AddAsync(first);
            await ctx1.SaveChangesAsync();
        }

        // Duplicate active Draft/Issued with same (Org, IdempotencyKey) MUST fail at DB level
        await using (var ctxDup = new SqliteRelationalApplicationDbContext(options, currentUser))
        {
            var duplicate = Invoice.CreateDraft(
                orgId, leaseId, propertyId, unitId, residentId,
                BillingPeriod.ForMonth(2026, 3), new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 5),
                "EGP", utcNow.AddMinutes(1), idempotencyKey: key);
            duplicate.AddLine(ChargeType.Rent, "March Rent", 1m, 12000m, utcNow.AddMinutes(1));
            await ctxDup.Invoices.AddAsync(duplicate);

            await Assert.ThrowsAsync<DbUpdateException>(() => ctxDup.SaveChangesAsync());
        }

        // After voiding first invoice, creating a new rent invoice with the same IdempotencyKey succeeds
        await using (var ctxVoid = new SqliteRelationalApplicationDbContext(options, currentUser))
        {
            var repo = new InvoiceRepository(ctxVoid);
            var existing = await repo.GetByIdAsync(firstId, orgId);
            existing!.Void("Wrong rent terms", utcNow.AddMinutes(2));
            await ctxVoid.SaveChangesAsync();

            var replacement = Invoice.CreateDraft(
                orgId, leaseId, propertyId, unitId, residentId,
                BillingPeriod.ForMonth(2026, 3), new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 5),
                "EGP", utcNow.AddMinutes(3), idempotencyKey: key);
            replacement.AddLine(ChargeType.Rent, "Corrected March Rent", 1m, 12500m, utcNow.AddMinutes(3));
            await repo.AddAsync(replacement);
            await ctxVoid.SaveChangesAsync();

            Assert.True(await repo.ExistsActiveByIdempotencyKeyAsync(key, orgId));
        }
    }

    [Fact]
    public async Task InvoiceNumberGenerator_ShouldProduceUniqueGaplessNumbersPerOrgAndYear_IncludingConcurrentCalls()
    {
        var tempDbPath = Path.Combine(Path.GetTempPath(), $"pm_seq_{Guid.NewGuid():N}.db");
        try
        {
            var connString = $"Data Source={tempDbPath};Default Timeout=5";
            var utcNow = new DateTime(2026, 3, 1, 10, 0, 0, DateTimeKind.Utc);
            var orgA = Organization.Create("Org A", utcNow);
            var orgB = Organization.Create("Org B", utcNow);

            var baseOptions = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connString).Options;
            await using (var initCtx = new SqliteRelationalApplicationDbContext(baseOptions, null))
            {
                await initCtx.Database.EnsureCreatedAsync();
                await initCtx.Organizations.AddRangeAsync(orgA, orgB);
                await initCtx.SaveChangesAsync();
            }

            // Concurrent allocation across 8 tasks on Org A for 2026
            var tasks = Enumerable.Range(0, 8).Select(async _ =>
            {
                await using var ctx = new SqliteRelationalApplicationDbContext(baseOptions, null);
                var gen = new InvoiceNumberGenerator(ctx);
                return await gen.GenerateNextAsync(orgA.Id, 2026);
            });

            var numbers2026OrgA = (await Task.WhenAll(tasks)).OrderBy(x => x).ToList();
            Assert.Equal(
                [
                    "INV-2026-000001", "INV-2026-000002", "INV-2026-000003", "INV-2026-000004",
                    "INV-2026-000005", "INV-2026-000006", "INV-2026-000007", "INV-2026-000008"
                ],
                numbers2026OrgA);

            // Different year & different org maintain independent sequences starting at 000001
            await using var seqCtx = new SqliteRelationalApplicationDbContext(baseOptions, null);
            var seqGen = new InvoiceNumberGenerator(seqCtx);
            Assert.Equal("INV-2027-000001", await seqGen.GenerateNextAsync(orgA.Id, 2027));
            Assert.Equal("INV-2026-000001", await seqGen.GenerateNextAsync(orgB.Id, 2026));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(tempDbPath))
            {
                File.Delete(tempDbPath);
            }
        }
    }

    [Fact]
    public async Task ImmutabilityAndTenantIsolation_ShouldBlockAdjustmentAndIssuedLineTampering_AndCrossTenantAccess()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var utcNow = new DateTime(2026, 3, 1, 10, 0, 0, DateTimeKind.Utc);
        var actorId = Guid.NewGuid();
        var userA = new TestCurrentUser { UserId = actorId, Role = "Owner" };
        var (orgA, propertyId, unitId, residentId, leaseId) = await SeedLeaseGraphAsync(connection, userA, utcNow);

        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        Guid invoiceId;

        await using (var ctx = new SqliteRelationalApplicationDbContext(options, userA))
        {
            var inv = Invoice.CreateDraft(
                orgA, leaseId, propertyId, unitId, residentId,
                BillingPeriod.ForMonth(2026, 3), new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 5), "EGP", utcNow);
            inv.AddLine(ChargeType.Rent, "March Rent", 1m, 12000m, utcNow);
            inv.Issue("INV-2026-000001", new DateOnly(2026, 3, 1), utcNow);
            inv.ApplyAdjustment(InvoiceAdjustmentType.Debit, ChargeType.LateFee, 500m, "Late Fee", "Late", actorId, utcNow.AddDays(6));
            invoiceId = inv.Id;

            await ctx.Invoices.AddAsync(inv);
            await ctx.SaveChangesAsync();
        }

        // Attempt direct line deletion & adjustment deletion on Issued invoice
        await using (var tamperCtx = new SqliteRelationalApplicationDbContext(options, userA))
        {
            var line = await tamperCtx.InvoiceLines.FirstAsync(l => l.InvoiceId == invoiceId);
            tamperCtx.InvoiceLines.Remove(line);
            await Assert.ThrowsAsync<InvalidOperationException>(() => tamperCtx.SaveChangesAsync());
        }

        await using (var tamperAdjCtx = new SqliteRelationalApplicationDbContext(options, userA))
        {
            var adj = await tamperAdjCtx.InvoiceAdjustments.FirstAsync(a => a.InvoiceId == invoiceId);
            tamperAdjCtx.InvoiceAdjustments.Remove(adj);
            await Assert.ThrowsAsync<InvalidOperationException>(() => tamperAdjCtx.SaveChangesAsync());
        }

        // Cross-tenant query isolation
        var userB = new TestCurrentUser { UserId = Guid.NewGuid(), OrganizationId = Guid.NewGuid(), Role = "Owner" };
        await using (var ctxB = new SqliteRelationalApplicationDbContext(options, userB))
        {
            Assert.Empty(await ctxB.Invoices.ToListAsync());
            Assert.Empty(await ctxB.InvoiceLines.ToListAsync());
            Assert.Empty(await ctxB.InvoiceAdjustments.ToListAsync());
        }
    }

    [Fact]
    public async Task BillingCheckConstraints_AndTransactionRollback_ShouldEnforceDbIntegrity()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var utcNow = new DateTime(2026, 3, 1, 10, 0, 0, DateTimeKind.Utc);
        var currentUser = new TestCurrentUser { UserId = Guid.NewGuid(), Role = "Owner" };
        var (orgId, propertyId, unitId, residentId, leaseId) = await SeedLeaseGraphAsync(connection, currentUser, utcNow);

        // Violate CK_Invoices_Totals (Total < 0)
        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = @"
                INSERT INTO Invoices (Id, OrganizationId, LeaseId, PropertyId, UnitId, ResidentId, InvoiceNumber, BillingPeriodStart, BillingPeriodEnd, IssueDate, DueDate, Currency, Status, Subtotal, TotalAdjustments, Total, AmountPaid, BalanceDue, CreatedAtUtc, RowVersion)
                VALUES ($id, $orgId, $leaseId, $propId, $unitId, $resId, NULL, '2026-03-01', '2026-03-31', '2026-03-01', '2026-03-05', 'EGP', 1, -100, 0, -100, 0, -100, '2026-03-01T10:00:00Z', X'');";
            cmd.Parameters.AddWithValue("$id", Guid.NewGuid());
            cmd.Parameters.AddWithValue("$orgId", orgId);
            cmd.Parameters.AddWithValue("$leaseId", leaseId);
            cmd.Parameters.AddWithValue("$propId", propertyId);
            cmd.Parameters.AddWithValue("$unitId", unitId);
            cmd.Parameters.AddWithValue("$resId", residentId);
            var ex = await Assert.ThrowsAsync<SqliteException>(() => cmd.ExecuteNonQueryAsync());
            Assert.Contains("CHECK constraint failed", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        // Violate CK_Invoices_IssuedNumber (Status = 2 Issued with NULL InvoiceNumber)
        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = @"
                INSERT INTO Invoices (Id, OrganizationId, LeaseId, PropertyId, UnitId, ResidentId, InvoiceNumber, BillingPeriodStart, BillingPeriodEnd, IssueDate, DueDate, Currency, Status, Subtotal, TotalAdjustments, Total, AmountPaid, BalanceDue, CreatedAtUtc, RowVersion)
                VALUES ($id, $orgId, $leaseId, $propId, $unitId, $resId, NULL, '2026-03-01', '2026-03-31', '2026-03-01', '2026-03-05', 'EGP', 2, 1000, 0, 1000, 0, 1000, '2026-03-01T10:00:00Z', X'');";
            cmd.Parameters.AddWithValue("$id", Guid.NewGuid());
            cmd.Parameters.AddWithValue("$orgId", orgId);
            cmd.Parameters.AddWithValue("$leaseId", leaseId);
            cmd.Parameters.AddWithValue("$propId", propertyId);
            cmd.Parameters.AddWithValue("$unitId", unitId);
            cmd.Parameters.AddWithValue("$resId", residentId);
            var ex = await Assert.ThrowsAsync<SqliteException>(() => cmd.ExecuteNonQueryAsync());
            Assert.Contains("CHECK constraint failed", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        // Verify transaction rollback reverts invoice, charge, and sequence number atomically
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        await using (var txCtx = new SqliteRelationalApplicationDbContext(options, currentUser))
        {
            await using var tx = await txCtx.BeginTransactionAsync();
            var gen = new InvoiceNumberGenerator(txCtx);
            var allocated = await gen.GenerateNextAsync(orgId, 2026);
            Assert.Equal("INV-2026-000001", allocated);

            var inv = Invoice.CreateDraft(
                orgId, leaseId, propertyId, unitId, residentId,
                BillingPeriod.ForMonth(2026, 3), new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 5), "EGP", utcNow);
            inv.AddLine(ChargeType.Rent, "March Rent", 1m, 12000m, utcNow);
            inv.Issue(allocated, new DateOnly(2026, 3, 1), utcNow);

            await txCtx.Invoices.AddAsync(inv);
            await txCtx.SaveChangesAsync();
            await tx.RollbackAsync();
        }

        await using (var verifyCtx = new SqliteRelationalApplicationDbContext(options, currentUser))
        {
            Assert.Empty(await verifyCtx.Invoices.ToListAsync());
            var gen = new InvoiceNumberGenerator(verifyCtx);
            // Because transaction rolled back, 000001 is available again with zero gaps
            Assert.Equal("INV-2026-000001", await gen.GenerateNextAsync(orgId, 2026));
        }
    }

    [Fact]
    public async Task InvoiceRepository_ExistsActiveRentOverlap_AndSummaryPaging_ShouldWorkAccurately()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var utcNow = new DateTime(2026, 3, 1, 10, 0, 0, DateTimeKind.Utc);
        var currentUser = new TestCurrentUser { UserId = Guid.NewGuid(), Role = "Owner" };
        var (orgId, propertyId, unitId, residentId, leaseId) = await SeedLeaseGraphAsync(connection, currentUser, utcNow);

        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        await using var ctx = new SqliteRelationalApplicationDbContext(options, currentUser);
        var repo = new InvoiceRepository(ctx);

        // Non-rent utility invoice should NOT block rent overlap
        var utilityInvoice = Invoice.CreateDraft(
            orgId, leaseId, propertyId, unitId, residentId,
            BillingPeriod.ForMonth(2026, 3), new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 5), "EGP", utcNow);
        utilityInvoice.AddLine(ChargeType.Utility, "Water", 1m, 200m, utcNow);
        await repo.AddAsync(utilityInvoice);
        await ctx.SaveChangesAsync();

        Assert.False(await repo.ExistsActiveRentOverlapAsync(leaseId, orgId, new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31)));

        // Active rent invoice blocks exact and partial overlapping periods
        var rentInvoice = Invoice.CreateDraft(
            orgId, leaseId, propertyId, unitId, residentId,
            BillingPeriod.ForMonth(2026, 3), new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 5), "EGP", utcNow);
        rentInvoice.AddLine(ChargeType.Rent, "March Rent", 1m, 12000m, utcNow);
        await repo.AddAsync(rentInvoice);
        await ctx.SaveChangesAsync();

        Assert.True(await repo.ExistsActiveRentOverlapAsync(leaseId, orgId, new DateOnly(2026, 3, 15), new DateOnly(2026, 4, 10)));
        Assert.False(await repo.ExistsActiveRentOverlapAsync(leaseId, orgId, new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 30)));

        // Summary query with IncludeDetails: false, AsNoTracking: true returns rows cleanly
        var (summaryItems, total) = await repo.GetPagedAsync(
            new InvoiceFilter(orgId, IncludeDetails: false, AsNoTracking: true));
        Assert.Equal(2, total);
        Assert.Equal(2, summaryItems.Count);
        Assert.Empty(summaryItems[0].Lines);

        // Voiding rent invoice clears overlap lock
        rentInvoice.Void("Replaced", utcNow.AddMinutes(5));
        await ctx.SaveChangesAsync();
        Assert.False(await repo.ExistsActiveRentOverlapAsync(leaseId, orgId, new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31)));
    }
}
