using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Options;
using PropertyManagement.Application.Abstractions.Payments;
using PropertyManagement.Application.Abstractions.Ledger;
using PropertyManagement.Application.Abstractions.Security;
using PropertyManagement.Application.Features.Payments.Lifecycle;
using PropertyManagement.Application.Features.Payments.Refunds;
using PropertyManagement.Application.Features.Payments.Webhooks;
using PropertyManagement.Domain.Entities.Billing;
using PropertyManagement.Domain.Entities.Organizations;
using PropertyManagement.Domain.Entities.Properties;
using PropertyManagement.Domain.Entities.Residents;
using PropertyManagement.Domain.Enums;
using PropertyManagement.Infrastructure.Common.Security;
using PropertyManagement.Infrastructure.Payments;
using PropertyManagement.Infrastructure.Persistence;
using PropertyManagement.Infrastructure.Persistence.Repositories;
using PropertyManagement.Infrastructure.Persistence.Repositories.Audit;
using PropertyManagement.Infrastructure.Persistence.Repositories.Billing;
using PropertyManagement.Infrastructure.Persistence.Repositories.Ledger;
using PropertyManagement.Infrastructure.Persistence.Repositories.Payments;
using DomainUnit = PropertyManagement.Domain.Entities.Properties.Unit;

namespace PropertyManagement.IntegrationTests;

public sealed class PaymentProvidersContractAndFlowTests
{
    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public List<(HttpMethod Method, Uri Uri, string? Body, string? IdempotencyKey)> RecordedRequests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is not null ? await request.Content.ReadAsStringAsync(cancellationToken) : null;
            var idem = request.Headers.TryGetValues("Idempotency-Key", out var vals) ? vals.FirstOrDefault() : null;
            RecordedRequests.Add((request.Method, request.RequestUri!, body, idem));
            return responder(request);
        }
    }

    private sealed class MutableClock(DateTime initialUtc) : PropertyManagement.Application.Abstractions.IClock
    {
        public DateTime UtcNow { get; set; } = initialUtc;
    }

    private sealed class TestCurrentUser : ICurrentUser
    {
        public Guid? UserId { get; set; }
        public Guid? OrganizationId { get; set; }
        public string? Role { get; set; }
        public string? Email { get; set; }
        public bool IsAuthenticated => UserId.HasValue;
    }

    private sealed class SqliteRelationalDbContext(
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
                    var rv = entityType.FindProperty("RowVersion");
                    if (rv is not null)
                    {
                        rv.IsConcurrencyToken = false;
                        rv.ValueGenerated = ValueGenerated.Never;
                        rv.SetDefaultValueSql("X''");
                    }
                }
            }
        }
    }

    [Fact]
    public void PaymentProviderFactory_ShouldResolveSupportedProviders_AndFallbackBankToManual()
    {
        var opts = Options.Create(new PaymentProviderOptions());
        var factory = new PaymentProviderFactory(
        [
            new ManualPaymentProvider(),
            new SimulatedPaymentProvider(opts),
            new StripePaymentProvider(new HttpClient(), opts),
            new PaymobPaymentProvider(new HttpClient(), opts)
        ]);

        Assert.IsType<ManualPaymentProvider>(factory.Resolve(PaymentProviderType.Manual));
        Assert.IsType<ManualPaymentProvider>(factory.Resolve(PaymentProviderType.Bank));
        Assert.IsType<SimulatedPaymentProvider>(factory.Resolve(PaymentProviderType.Simulated));
        Assert.IsType<StripePaymentProvider>(factory.Resolve(PaymentProviderType.Stripe));
        Assert.IsType<PaymobPaymentProvider>(factory.Resolve(PaymentProviderType.Paymob));
        Assert.Throws<NotSupportedException>(() => factory.Resolve(PaymentProviderType.Adyen));
    }

    [Fact]
    public async Task SimulatedProvider_ShouldVerifySignature_RejectReplay_AndTrackSettlements()
    {
        var now = new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc);
        var opts = Options.Create(new PaymentProviderOptions
        {
            WebhookToleranceSeconds = 300,
            Simulated = new SimulatedProviderOptions { WebhookSecret = "sim_secret_123", FeePercent = 0.025m, FixedFee = 5m }
        });

        var sim = new SimulatedPaymentProvider(opts);
        var payRes = await sim.CreateOrCapturePaymentAsync(new ProviderPaymentRequest(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1000m, "EGP", PaymentMethod.Card, "k1"));

        Assert.True(payRes.CallSucceeded);
        Assert.True(payRes.IsPaid);
        Assert.Equal(30m, payRes.ProviderFee);

        var settlements = await sim.FetchSettlementReportAsync(DateTime.UtcNow.AddMinutes(-5), DateTime.UtcNow.AddMinutes(5));
        Assert.Single(settlements);

        var body = """{"eventId":"evt_sim_1","eventType":"payment.succeeded","providerPaymentId":"sim_pay_1","amount":1000}""";
        var validTs = new DateTimeOffset(now.AddSeconds(-60)).ToUnixTimeSeconds();
        var validSig = SimulatedPaymentProvider.ComputeWebhookSignature("sim_secret_123", validTs, body);
        var verified = sim.VerifyAndParseWebhook(body, new Dictionary<string, string> { ["X-Simulated-Signature"] = validSig }, now);
        Assert.True(verified.IsSignatureValid);
        Assert.True(verified.IsWithinReplayWindow);
        Assert.Equal("evt_sim_1", verified.EventId);

        var staleTs = new DateTimeOffset(now.AddMinutes(-20)).ToUnixTimeSeconds();
        var staleSig = SimulatedPaymentProvider.ComputeWebhookSignature("sim_secret_123", staleTs, body);
        var replay = sim.VerifyAndParseWebhook(body, new Dictionary<string, string> { ["X-Simulated-Signature"] = staleSig }, now);
        Assert.True(replay.IsSignatureValid);
        Assert.False(replay.IsWithinReplayWindow);
    }

    [Fact]
    public async Task StripeProvider_HttpContractAndWebhookVerification_ShouldWorkWithMockHttp()
    {
        var now = new DateTime(2026, 7, 1, 14, 0, 0, DateTimeKind.Utc);
        var opts = Options.Create(new PaymentProviderOptions
        {
            WebhookToleranceSeconds = 300,
            Stripe = new StripeProviderOptions
            {
                BaseUrl = "https://stripe.mock.local",
                SecretKey = "sk_test_mock",
                WebhookSecret = "whsec_stripe_mock"
            }
        });

        var handler = new StubHttpMessageHandler(req =>
        {
            var path = req.RequestUri!.AbsolutePath;
            if (path.EndsWith("/v1/payment_intents") && req.Method == HttpMethod.Post)
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""{"id":"pi_123","client_secret":"cs_123","customer":"cus_1","status":"succeeded","latest_charge":{"id":"ch_123","balance_transaction":{"id":"txn_123","fee":290}}}""")
                };
            }
            if (path.EndsWith("/v1/refunds") && req.Method == HttpMethod.Post)
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""{"id":"re_123","status":"succeeded"}""")
                };
            }
            if (path.EndsWith("/v1/balance_transactions") && req.Method == HttpMethod.Get)
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""{"data":[{"id":"txn_123","amount":10000,"fee":290,"net":9710,"currency":"egp","source":"ch_123","created":1782914400}]}""")
                };
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var stripe = new StripePaymentProvider(new HttpClient(handler), opts);
        var cap = await stripe.CreateOrCapturePaymentAsync(new ProviderPaymentRequest(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 100m, "EGP", PaymentMethod.Card, "stripe-idem-1"));

        Assert.True(cap.CallSucceeded);
        Assert.True(cap.IsPaid);
        Assert.Equal("pi_123", cap.ProviderPaymentId);
        Assert.Equal("txn_123", cap.ProviderTransactionId);
        Assert.Equal(2.90m, cap.ProviderFee);
        Assert.Equal("stripe-idem-1", handler.RecordedRequests[0].IdempotencyKey);

        var refund = await stripe.RefundPaymentAsync(new ProviderRefundRequest(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "pi_123", 40m, "EGP", "Duplicate", "ref-idem-1"));
        Assert.True(refund.CallSucceeded);
        Assert.True(refund.IsCompleted);
        Assert.Equal("re_123", refund.ProviderRefundId);

        var report = await stripe.FetchSettlementReportAsync(now.AddDays(-1), now.AddDays(1));
        Assert.Single(report);
        Assert.Equal(97.10m, report[0].NetAmount);

        var rawWebhook = """{"id":"evt_stripe_1","type":"payment_intent.succeeded","created":1782914400,"data":{"object":{"id":"pi_123","object":"payment_intent","status":"succeeded","amount_received":10000,"currency":"egp","latest_charge":"ch_123"}}}""";
        var ts = new DateTimeOffset(now).ToUnixTimeSeconds();
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes("whsec_stripe_mock"));
        var v1 = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes($"{ts}.{rawWebhook}"))).ToLowerInvariant();
        var verified = stripe.VerifyAndParseWebhook(rawWebhook, new Dictionary<string, string> { ["Stripe-Signature"] = $"t={ts},v1={v1}" }, now);
        Assert.True(verified.IsSignatureValid);
        Assert.True(verified.IsWithinReplayWindow);
        Assert.Equal("evt_stripe_1", verified.EventId);
        Assert.Equal("pi_123", verified.ProviderPaymentId);
    }

    [Fact]
    public async Task PaymobProvider_HttpContractAndHmacSha512Webhook_ShouldWorkWithMockHttp()
    {
        var now = new DateTime(2026, 7, 1, 15, 0, 0, DateTimeKind.Utc);
        var opts = Options.Create(new PaymentProviderOptions
        {
            WebhookToleranceSeconds = 300,
            Paymob = new PaymobProviderOptions
            {
                BaseUrl = "https://paymob.mock.local/api",
                ApiKey = "pm_api_key",
                HmacSecret = "pm_hmac_secret",
                IntegrationId = "999111",
                IframeId = "555"
            }
        });

        var handler = new StubHttpMessageHandler(req =>
        {
            var path = req.RequestUri!.AbsolutePath;
            if (path.EndsWith("/auth/tokens"))
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("""{"token":"pm_jwt_tok"}""") };
            if (path.EndsWith("/ecommerce/orders"))
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("""{"id":887766}""") };
            if (path.EndsWith("/acceptance/payment_keys"))
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("""{"token":"pm_pay_key_1"}""") };
            if (path.EndsWith("/acceptance/void_refund/refund"))
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("""{"id":991122,"success":true}""") };
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var paymob = new PaymobPaymentProvider(new HttpClient(handler), opts);
        var intent = await paymob.CreateIntentAsync(new ProviderCreateIntentRequest(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1500m, "EGP", PaymentMethod.Card, "pm-1"));

        Assert.True(intent.Succeeded);
        Assert.Equal("887766", intent.ProviderIntentId);
        Assert.Equal("pm_pay_key_1", intent.ClientSecret);
        Assert.Contains("iframes/555", intent.ActionUrl);

        var createdAt = now.ToString("yyyy-MM-ddTHH:mm:ssZ");
        var concatenated = $"150000{createdAt}EGPfalsefalse445566999111truefalsefalsefalsefalsefalse88776677false1234VisaCardtrue";
        using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes("pm_hmac_secret"));
        var expectedHmac = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(concatenated))).ToLowerInvariant();

        var rawPaymobWebhook = $$"""
        {
          "obj": {
            "id": 445566,
            "pending": false,
            "amount_cents": 150000,
            "success": true,
            "is_auth": false,
            "is_capture": false,
            "is_standalone_payment": false,
            "is_voided": false,
            "is_refunded": false,
            "is_3d_secure": true,
            "integration_id": 999111,
            "has_parent_transaction": false,
            "order": { "id": 887766 },
            "created_at": "{{createdAt}}",
            "currency": "EGP",
            "error_occured": false,
            "owner": 77,
            "source_data": { "type": "Card", "pan": "1234", "sub_type": "Visa" }
          }
        }
        """;

        var verified = paymob.VerifyAndParseWebhook(rawPaymobWebhook, new Dictionary<string, string> { ["hmac"] = expectedHmac }, now);
        Assert.True(verified.IsSignatureValid);
        Assert.True(verified.IsWithinReplayWindow);
        Assert.Equal("payment.succeeded", verified.EventType);
        Assert.Equal("887766", verified.ProviderPaymentId);
        Assert.Equal("445566", verified.ProviderTransactionId);
        Assert.Equal(1500m, verified.Amount);
    }

    [Fact]
    public async Task EndToEnd_SqlBacked_PendingPayment_WebhookSuccess_Allocation_AndPartialRefund_ShouldPreserveDbInvariants()
    {
        var now = new DateTime(2026, 7, 1, 10, 0, 0, DateTimeKind.Utc);
        using var conn = new SqliteConnection("Data Source=:memory:");
        await conn.OpenAsync();
        var dbOpts = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(conn).Options;

        var currentUser = new TestCurrentUser { UserId = Guid.NewGuid(), Role = nameof(MembershipRole.Owner), Email = "owner@e2e.local" };
        var systemTenant = new SystemTenantContext();
        Guid orgId, residentId, invoiceId;

        await using (var setup = new SqliteRelationalDbContext(dbOpts, currentUser, systemTenant))
        {
            await setup.Database.EnsureCreatedAsync();
            var org = Organization.Create("E2E Towers", now, defaultCurrency: "EGP");
            currentUser.OrganizationId = org.Id;
            orgId = org.Id;
            var prop = Property.Create(orgId, "Maadi Court", "1 Road 9", PropertyType.Residential, now);
            var unit = DomainUnit.Create(orgId, prop.Id, "M-1", UnitType.Apartment, 6000m, now);
            var resident = Resident.Create(orgId, "Salma", "Adel", "01099887766", now);
            residentId = resident.Id;
            var lease = PropertyManagement.Domain.Entities.Leases.Lease.CreateDraft(
                orgId, prop.Id, unit.Id, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31),
                new PropertyManagement.Domain.Entities.Leases.LeaseFinancialTerms(6000m, 6000m, RentFrequency.Monthly, 5, currency: "EGP"), now);
            lease.AddResident(residentId, LeaseResidentRole.PrimaryTenant, now);
            lease.SubmitForApproval(now);
            lease.Approve(now);

            var inv = Invoice.CreateDraft(orgId, lease.Id, prop.Id, unit.Id, residentId,
                BillingPeriod.ForMonth(2026, 7), new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 5), "EGP", now);
            inv.AddLine(ChargeType.Rent, "July Rent", 1m, 4000m, now);
            inv.Issue("INV-E2E-1", new DateOnly(2026, 7, 1), now);
            invoiceId = inv.Id;

            await setup.Organizations.AddAsync(org);
            await setup.Properties.AddAsync(prop);
            await setup.Units.AddAsync(unit);
            await setup.Residents.AddAsync(resident);
            await setup.Leases.AddAsync(lease);
            await setup.Invoices.AddAsync(inv);
            await setup.SaveChangesAsync();
        }

        var providerOpts = Options.Create(new PaymentProviderOptions
        {
            WebhookToleranceSeconds = 300,
            Simulated = new SimulatedProviderOptions { WebhookSecret = "e2e_sim_secret", FeePercent = 0.02m, FixedFee = 0m }
        });
        var simProvider = new SimulatedPaymentProvider(providerOpts);
        var factory = new PaymentProviderFactory([new ManualPaymentProvider(), simProvider]);
        var clock = new MutableClock(now);

        Guid createdPaymentId;
        string providerPaymentId;

        await using (var db = new SqliteRelationalDbContext(dbOpts, currentUser, systemTenant))
        {
            var handler = new CreatePaymentCommandHandler(
                new PaymentRepository(db), new PaymentIntentRepository(db), new PaymentNumberGenerator(db),
                new ResidentRepository(db), new LeaseRepository(db), new InvoiceRepository(db),
                new LedgerRepository(db), new JournalEntryNumberGenerator(db), new FinancialAuditLogRepository(db),
                factory, db, clock);

            var res = await handler.Handle(
                new CreatePaymentCommand(
                    orgId, currentUser.UserId!.Value, residentId, 5000m, "EGP",
                    PaymentMethod.Card, PaymentProviderType.Simulated,
                    OverpaymentPolicy: OverpaymentPolicy.AutoApplyToOldestInvoices,
                    IdempotencyKey: "e2e-pay-1",
                    Reference: "simulate:processing"),
                CancellationToken.None);

            Assert.True(res.IsSuccess);
            Assert.Equal(PaymentStatus.Processing, res.Value.Status);
            createdPaymentId = res.Value.Id;
            providerPaymentId = res.Value.ProviderPaymentId!;
        }

        var anonUser = new TestCurrentUser();
        await using (var webhookDb = new SqliteRelationalDbContext(dbOpts, anonUser, systemTenant))
        {
            var whHandler = new ProcessPaymentWebhookCommandHandler(
                factory, new WebhookEventRepository(webhookDb), new PaymentRepository(webhookDb),
                new PaymentIntentRepository(webhookDb), new InvoiceRepository(webhookDb),
                new LedgerRepository(webhookDb), new JournalEntryNumberGenerator(webhookDb),
                new FinancialAuditLogRepository(webhookDb), systemTenant, webhookDb, clock);

            var payload = $$"""{"eventId":"evt_e2e_1","eventType":"payment.succeeded","providerPaymentId":"{{providerPaymentId}}","providerTransactionId":"sim_tx_e2e_1","amount":5000,"feeAmount":100,"currency":"EGP"}""";
            var sig = SimulatedPaymentProvider.ComputeWebhookSignature("e2e_sim_secret", new DateTimeOffset(now).ToUnixTimeSeconds(), payload);

            var whRes = await whHandler.Handle(
                new ProcessPaymentWebhookCommand(PaymentProviderType.Simulated, payload, new Dictionary<string, string> { ["X-Simulated-Signature"] = sig }),
                CancellationToken.None);

            Assert.True(whRes.IsSuccess);
            Assert.Equal(WebhookEventStatus.Processed, whRes.Value.Status);

            var dupRes = await whHandler.Handle(
                new ProcessPaymentWebhookCommand(PaymentProviderType.Simulated, payload, new Dictionary<string, string> { ["X-Simulated-Signature"] = sig }),
                CancellationToken.None);
            Assert.True(dupRes.IsSuccess);
            Assert.True(dupRes.Value.IsDuplicate);
        }

        await using (var refDb = new SqliteRelationalDbContext(dbOpts, currentUser, systemTenant))
        {
            var refHandler = new RefundPaymentCommandHandler(
                new PaymentRepository(refDb), new LedgerRepository(refDb), new JournalEntryNumberGenerator(refDb),
                new FinancialAuditLogRepository(refDb), factory, refDb, clock);

            var refRes = await refHandler.Handle(
                new RefundPaymentCommand(orgId, currentUser.UserId!.Value, createdPaymentId, 1000m, "Refund unapplied balance", IdempotencyKey: "e2e-ref-1"),
                CancellationToken.None);

            Assert.True(refRes.IsSuccess);
            Assert.Equal(RefundStatus.Succeeded, refRes.Value.Status);

            var finalPay = await new PaymentRepository(refDb).GetByIdAsync(createdPaymentId, orgId);
            Assert.NotNull(finalPay);
            Assert.Equal(PaymentStatus.PartiallyRefunded, finalPay!.Status);
            Assert.Equal(4000m, finalPay.AllocatedAmount);
            Assert.Equal(1000m, finalPay.RefundedAmount);
            Assert.Equal(0m, finalPay.UnappliedAmount);

            var finalInv = await new InvoiceRepository(refDb).GetByIdAsync(invoiceId, orgId);
            Assert.Equal(InvoiceStatus.Paid, finalInv!.Status);

            var ledgerRepo = new LedgerRepository(refDb);
            var (entries, totalEntries) = await ledgerRepo.GetPagedAsync(new LedgerFilter(orgId, PageNumber: 1, PageSize: 20));
            Assert.Equal(2, totalEntries);
            Assert.All(entries, e => Assert.Equal(e.TotalDebits, e.TotalCredits));
        }
    }
}
