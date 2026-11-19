using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using PropertyManagement.Application.Abstractions.Security;
using PropertyManagement.Domain.Entities.Audit;
using PropertyManagement.Domain.Entities.Billing;
using PropertyManagement.Domain.Entities.Ledger;
using PropertyManagement.Domain.Entities.Ledger.Services;
using PropertyManagement.Domain.Entities.Organizations;
using PropertyManagement.Domain.Entities.Payments;
using PropertyManagement.Domain.Entities.Properties;
using PropertyManagement.Domain.Entities.Residents;
using PropertyManagement.Domain.Enums;
using PropertyManagement.Infrastructure.Common.Security;
using PropertyManagement.Infrastructure.Persistence;
using PropertyManagement.Infrastructure.Persistence.Repositories.Audit;
using PropertyManagement.Infrastructure.Persistence.Repositories.Billing;
using PropertyManagement.Infrastructure.Persistence.Repositories.Ledger;
using PropertyManagement.Infrastructure.Persistence.Repositories.Payments;
using DomainUnit = PropertyManagement.Domain.Entities.Properties.Unit;

namespace PropertyManagement.IntegrationTests;

public sealed class PaymentPersistenceIntegrationTests
{
    private sealed class TestCurrentUser : ICurrentUser
    {
        public Guid? UserId { get; set; }
        public Guid? OrganizationId { get; set; }
        public string? Role { get; set; }
        public string? Email { get; set; }
        public bool IsAuthenticated => UserId.HasValue;
    }

    private sealed class SqliteRelationalApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options,
        ICurrentUser? currentUser = null,
        ISystemTenantContext? systemTenantContext = null)
        : ApplicationDbContext(options, currentUser, null, systemTenantContext)
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

    private static async Task<(SqliteConnection Connection, DbContextOptions<ApplicationDbContext> Options, TestCurrentUser CurrentUser, SystemTenantContext SystemTenant, Guid OrgId, Guid ResidentId, Invoice InvoiceA, Invoice InvoiceB)> CreateSeededContextAsync(DateTime utcNow)
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        var currentUser = new TestCurrentUser
        {
            UserId = Guid.NewGuid(),
            Role = nameof(MembershipRole.Owner),
            Email = "owner@test.local"
        };
        var systemTenant = new SystemTenantContext();

        await using (var setup = new SqliteRelationalApplicationDbContext(options, currentUser, systemTenant))
        {
            await setup.Database.EnsureCreatedAsync();

            var org = Organization.Create("Nile Payment Org", utcNow, defaultCurrency: "EGP");
            currentUser.OrganizationId = org.Id;

            var property = Property.Create(org.Id, "Zamalek Suites", "10 Brazil St", PropertyType.Residential, utcNow);
            var unit = DomainUnit.Create(org.Id, property.Id, "Z-101", UnitType.Apartment, 10000m, utcNow);
            var resident = Resident.Create(org.Id, "Karim", "Hassan", "01000000001", utcNow);

            var lease = PropertyManagement.Domain.Entities.Leases.Lease.CreateDraft(
                org.Id, property.Id, unit.Id,
                new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31),
                new PropertyManagement.Domain.Entities.Leases.LeaseFinancialTerms(10000m, 10000m, RentFrequency.Monthly, dueDayOfMonth: 5, currency: "EGP"),
                utcNow);
            lease.AddResident(resident.Id, LeaseResidentRole.PrimaryTenant, utcNow);
            lease.SubmitForApproval(utcNow);
            lease.Approve(utcNow);

            var invA = Invoice.CreateDraft(
                org.Id, lease.Id, property.Id, unit.Id, resident.Id,
                BillingPeriod.ForMonth(2026, 3), new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 5), "EGP", utcNow);
            invA.AddLine(ChargeType.Rent, "March Rent", 1m, 5000m, utcNow);
            invA.Issue("INV-2026-000001", new DateOnly(2026, 3, 1), utcNow);

            var invB = Invoice.CreateDraft(
                org.Id, lease.Id, property.Id, unit.Id, resident.Id,
                BillingPeriod.ForMonth(2026, 4), new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 5), "EGP", utcNow);
            invB.AddLine(ChargeType.Rent, "April Rent", 1m, 7000m, utcNow);
            invB.Issue("INV-2026-000002", new DateOnly(2026, 4, 1), utcNow);

            await setup.Organizations.AddAsync(org);
            await setup.Properties.AddAsync(property);
            await setup.Units.AddAsync(unit);
            await setup.Residents.AddAsync(resident);
            await setup.Leases.AddAsync(lease);
            await setup.Invoices.AddRangeAsync(invA, invB);
            await setup.SaveChangesAsync();

            return (connection, options, currentUser, systemTenant, org.Id, resident.Id, invA, invB);
        }
    }

    [Fact]
    public async Task Payment_Allocation_Ledger_Settlement_And_AccountBalances_ShouldPersistEndToEnd()
    {
        var now = new DateTime(2026, 4, 10, 12, 0, 0, DateTimeKind.Utc);
        var (conn, opts, currentUser, systemTenant, orgId, residentId, invA, invB) = await CreateSeededContextAsync(now);
        await using var _ = conn;

        Guid paymentId;
        await using (var db = new SqliteRelationalApplicationDbContext(opts, currentUser, systemTenant))
        {
            var intentRepo = new PaymentIntentRepository(db);
            var payRepo = new PaymentRepository(db);
            var payNumGen = new PaymentNumberGenerator(db);
            var invRepo = new InvoiceRepository(db);
            var ledgerRepo = new LedgerRepository(db);
            var jeNumGen = new JournalEntryNumberGenerator(db);
            var auditRepo = new FinancialAuditLogRepository(db);

            var intent = PaymentIntent.Create(
                orgId, residentId, 15000m, "EGP", PaymentMethod.Card, PaymentProviderType.Stripe, now.AddMinutes(30), now,
                idempotencyKey: "pi-req-1");
            intent.AttachProviderSession("pi_stripe_1", "sec_1", null, requiresAction: false, now);
            intent.MarkSucceeded(now.AddSeconds(5));
            await intentRepo.AddAsync(intent);

            var payment = Payment.Create(
                orgId, residentId, 15000m, "EGP", PaymentMethod.Card, PaymentProviderType.Stripe, now,
                paymentIntentId: intent.Id, idempotencyKey: "pay-req-1");
            payment.AssignPaymentNumber(await payNumGen.GenerateNextAsync(orgId, now));
            payment.AttachExternalIdentifiers("pay_stripe_1", "cus_1", "tx_stripe_1", "succeeded", now);
            payment.MarkSucceeded(now, providerTransactionId: "tx_stripe_1", confirmedByUserId: currentUser.UserId);

            var trackedInvoices = await invRepo.GetOutstandingByResidentAsync(residentId, orgId, "EGP");
            Assert.Equal(2, trackedInvoices.Count);

            var dbInvA = trackedInvoices.First(i => i.Id == invA.Id);
            var dbInvB = trackedInvoices.First(i => i.Id == invB.Id);

            payment.AllocateToInvoice(dbInvA.Id, 5000m, dbInvA.Currency, now, currentUser.UserId);
            dbInvA.RecordPaymentAllocation(5000m, now);

            payment.AllocateToInvoice(dbInvB.Id, 7000m, dbInvB.Currency, now, currentUser.UserId);
            dbInvB.RecordPaymentAllocation(7000m, now);

            await payRepo.AddAsync(payment);
            paymentId = payment.Id;

            var receiptJe = LedgerPostingService.CreatePaymentReceiptEntry(
                payment,
                await jeNumGen.GenerateNextAsync(orgId, now),
                $"receipt:{payment.Id}",
                allocatedAmount: 12000m,
                now);
            await ledgerRepo.AddAsync(receiptJe);

            payment.RecordSettlement(350m, now.AddDays(1), now.AddDays(1));
            var settleJe = LedgerPostingService.CreateProviderSettlementEntry(
                payment,
                await jeNumGen.GenerateNextAsync(orgId, now.AddDays(1)),
                $"settle:{payment.Id}",
                now.AddDays(1));
            await ledgerRepo.AddAsync(settleJe);

            await auditRepo.AddAsync(FinancialAuditLog.Record(
                orgId, "Payment.Settled", "Payment", payment.Id, "{\"fee\":350}", now.AddDays(1), currentUser.UserId));

            await db.SaveChangesAsync();
        }

        await using (var verify = new SqliteRelationalApplicationDbContext(opts, currentUser, systemTenant))
        {
            var payRepo = new PaymentRepository(verify);
            var ledgerRepo = new LedgerRepository(verify);

            var persisted = await payRepo.GetByIdAsync(paymentId, orgId);
            Assert.NotNull(persisted);
            Assert.Equal("PAY-2026-000001", persisted.PaymentNumber);
            Assert.Equal(12000m, persisted.AllocatedAmount);
            Assert.Equal(3000m, persisted.UnappliedAmount);
            Assert.Equal(350m, persisted.ProviderFee);
            Assert.Equal(14650m, persisted.NetSettlementAmount);
            Assert.Equal(2, persisted.Allocations.Count);

            var balances = await ledgerRepo.GetAccountBalancesAsync(orgId);
            Assert.Equal(14650m, balances.Single(b => b.AccountCode == LedgerAccountCode.Cash).NetBalance);
            Assert.Equal(350m, balances.Single(b => b.AccountCode == LedgerAccountCode.ProcessingFeeExpense).NetBalance);
            Assert.Equal(0m, balances.Single(b => b.AccountCode == LedgerAccountCode.PaymentProviderClearing).NetBalance);
            Assert.Equal(3000m, balances.Single(b => b.AccountCode == LedgerAccountCode.CustomerUnappliedCredit).NetBalance);
        }
    }

    [Fact]
    public async Task UniqueConstraints_AndImmutabilityGuards_ShouldEnforceFinancialIntegrity()
    {
        var now = new DateTime(2026, 4, 12, 9, 0, 0, DateTimeKind.Utc);
        var (conn, opts, currentUser, systemTenant, orgId, residentId, invA, _) = await CreateSeededContextAsync(now);
        await using var _ = conn;

        Guid paymentId;
        Guid journalEntryId;

        await using (var db = new SqliteRelationalApplicationDbContext(opts, currentUser, systemTenant))
        {
            var pay = Payment.Create(
                orgId, residentId, 5000m, "EGP", PaymentMethod.Cash, PaymentProviderType.Manual, now,
                idempotencyKey: "dup-pay-key");
            pay.MarkSucceeded(now, confirmedByUserId: currentUser.UserId);
            pay.AllocateToInvoice(invA.Id, 2000m, "EGP", now, currentUser.UserId);
            await db.Payments.AddAsync(pay);
            paymentId = pay.Id;

            var je = LedgerPostingService.CreatePaymentReceiptEntry(pay, "JE-2026-000001", "dup-je-key", 2000m, now);
            await db.JournalEntries.AddAsync(je);
            journalEntryId = je.Id;

            await db.ProcessedWebhookEvents.AddAsync(ProcessedWebhookEvent.CreateReceived(
                PaymentProviderType.Stripe, "evt_unique_1", "payment_intent.succeeded", "hash123", now, now, orgId));

            await db.FinancialAuditLogs.AddAsync(FinancialAuditLog.Record(
                orgId, "Payment.Created", "Payment", pay.Id, "{}", now, currentUser.UserId));

            await db.SaveChangesAsync();
        }

        // 1. Duplicate webhook event ID rejected by DB unique constraint
        await using (var db = new SqliteRelationalApplicationDbContext(opts, currentUser, systemTenant))
        {
            await db.ProcessedWebhookEvents.AddAsync(ProcessedWebhookEvent.CreateReceived(
                PaymentProviderType.Stripe, "evt_unique_1", "payment_intent.succeeded", "hash123", now, now, orgId));
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }

        // 2. Duplicate payment IdempotencyKey rejected by DB unique constraint
        await using (var db = new SqliteRelationalApplicationDbContext(opts, currentUser, systemTenant))
        {
            var dupPay = Payment.Create(
                orgId, residentId, 5000m, "EGP", PaymentMethod.Cash, PaymentProviderType.Manual, now,
                idempotencyKey: "dup-pay-key");
            await db.Payments.AddAsync(dupPay);
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }

        // 3. Duplicate ledger posting key rejected by DB unique constraint
        await using (var db = new SqliteRelationalApplicationDbContext(opts, currentUser, systemTenant))
        {
            var pay = await db.Payments.FirstAsync(p => p.Id == paymentId);
            var dupJe = LedgerPostingService.CreatePaymentReceiptEntry(pay, "JE-2026-000002", "dup-je-key", 2000m, now);
            await db.JournalEntries.AddAsync(dupJe);
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }

        // 4. Cannot delete payment, allocation, or audit log
        await using (var db = new SqliteRelationalApplicationDbContext(opts, currentUser, systemTenant))
        {
            var pay = await db.Payments.FirstAsync(p => p.Id == paymentId);
            db.Payments.Remove(pay);
            await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        }

        await using (var db = new SqliteRelationalApplicationDbContext(opts, currentUser, systemTenant))
        {
            var alloc = await db.PaymentAllocations.FirstAsync(a => a.PaymentId == paymentId);
            db.PaymentAllocations.Remove(alloc);
            await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        }

        await using (var db = new SqliteRelationalApplicationDbContext(opts, currentUser, systemTenant))
        {
            var log = await db.FinancialAuditLogs.FirstAsync();
            db.FinancialAuditLogs.Remove(log);
            await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        }

        // 5. Cannot delete posted JournalEntry
        await using (var db = new SqliteRelationalApplicationDbContext(opts, currentUser, systemTenant))
        {
            var je = await db.JournalEntries.FirstAsync(j => j.Id == journalEntryId);
            db.JournalEntries.Remove(je);
            await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        }
    }

    [Fact]
    public async Task SystemTenantContext_ShouldAllowWebhookWriteToTargetOrg_AndBlockCrossOrgWrite()
    {
        var now = new DateTime(2026, 4, 15, 8, 0, 0, DateTimeKind.Utc);
        var (conn, opts, _, systemTenant, orgId, residentId, _, _) = await CreateSeededContextAsync(now);
        await using var _ = conn;

        var unauthUser = new TestCurrentUser { UserId = null, OrganizationId = null };
        var otherOrgId = Guid.NewGuid();

        using (systemTenant.BeginScope(orgId))
        {
            await using var db = new SqliteRelationalApplicationDbContext(opts, unauthUser, systemTenant);
            var validPay = Payment.Create(orgId, residentId, 1500m, "EGP", PaymentMethod.Card, PaymentProviderType.Stripe, now);
            await db.Payments.AddAsync(validPay);
            await db.SaveChangesAsync();

            var crossPay = Payment.Create(otherOrgId, residentId, 1500m, "EGP", PaymentMethod.Card, PaymentProviderType.Stripe, now);
            await db.Payments.AddAsync(crossPay);
            await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        }
    }
}
