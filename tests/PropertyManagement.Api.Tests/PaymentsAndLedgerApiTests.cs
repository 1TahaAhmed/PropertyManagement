using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using PropertyManagement.Application.Abstractions.Authentication;
using PropertyManagement.Application.Abstractions.Ledger;
using PropertyManagement.Application.Abstractions.Payments;
using PropertyManagement.Application.Common.Models;
using PropertyManagement.Application.Features.Billing.Responses;
using PropertyManagement.Application.Features.Leases.Responses;
using PropertyManagement.Application.Features.Organizations.Responses;
using PropertyManagement.Application.Features.Payments.Responses;
using PropertyManagement.Application.Features.Properties.Response;
using PropertyManagement.Application.Features.Residents.Responses;
using PropertyManagement.Application.Features.Units.Response;
using PropertyManagement.Domain.Entities.Payments.Services;
using PropertyManagement.Domain.Enums;
using PropertyManagement.Infrastructure.Payments;
using PropertyManagement.Infrastructure.Persistence;
using Xunit;

namespace PropertyManagement.Api.Tests;

public sealed class PaymentsAndLedgerApiTests
{
    private const string SimWebhookSecret = "test_sim_whsec_12345";

    private sealed class TestApiFactory : WebApplicationFactory<Program>
    {
        private readonly string _databaseName = $"PropertyManagement_PaymentsApi_{Guid.NewGuid():N}";

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("ConnectionStrings:MyConn", @"Server=(localdb)\mssqllocaldb;Database=TestIgnored;");
            builder.UseSetting("Jwt:Secret", "super-secret-integration-test-signing-key-at-least-32-chars");
            builder.UseSetting("Jwt:Issuer", "PropertyManagement");
            builder.UseSetting("Jwt:Audience", "PropertyManagement");
            builder.UseSetting("Jwt:ExpiryMinutes", "15");
            builder.UseSetting("Jwt:RefreshTokenExpiryDays", "7");
            builder.UseSetting("Payments:WebhookToleranceSeconds", "300");
            builder.UseSetting("Payments:Simulated:WebhookSecret", SimWebhookSecret);
            builder.UseSetting("Payments:Simulated:FeePercent", "0.025");
            builder.UseSetting("Payments:Simulated:FixedFee", "2.00");

            builder.ConfigureServices(services =>
            {
                var descriptorsToRemove = services
                    .Where(d =>
                        d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>) ||
                        d.ServiceType == typeof(DbContextOptions) ||
                        (d.ServiceType.IsGenericType &&
                         d.ServiceType.GetGenericTypeDefinition().FullName?.Contains("IDbContextOptionsConfiguration") == true &&
                         d.ServiceType.GenericTypeArguments.Contains(typeof(ApplicationDbContext))))
                    .ToList();

                foreach (var descriptor in descriptorsToRemove)
                {
                    services.Remove(descriptor);
                }

                services.AddDbContext<ApplicationDbContext>(options =>
                    options
                        .UseInMemoryDatabase(_databaseName)
                        .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)));
            });
        }

        public HttpClient CreateAuthenticatedClient(Guid userId, Guid organizationId, MembershipRole role)
        {
            using var scope = Services.CreateScope();
            var jwtGenerator = scope.ServiceProvider.GetRequiredService<IJwtTokenGenerator>();
            var (token, _) = jwtGenerator.GenerateAccessToken(
                userId,
                $"user-{userId:N}@example.com",
                "Test",
                "User",
                organizationId,
                role.ToString());

            var client = CreateClient(new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false
            });
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return client;
        }
    }

    private static async Task<(Guid OrgId, Guid ResidentId, LeaseResponse Lease)> SeedActiveLeaseAsync(
        TestApiFactory factory,
        string defaultCurrency = "EGP",
        decimal rentAmount = 1000m)
    {
        var ownerId = Guid.NewGuid();
        var bootstrapClient = factory.CreateAuthenticatedClient(ownerId, Guid.NewGuid(), MembershipRole.Owner);
        var orgRes = await bootstrapClient.PostAsJsonAsync("/api/v1/organizations", new
        {
            name = $"PayOrg-{Guid.NewGuid():N}",
            defaultCurrency
        });
        Assert.Equal(HttpStatusCode.Created, orgRes.StatusCode);
        var org = await orgRes.Content.ReadFromJsonAsync<OrganizationResponse>();
        Assert.NotNull(org);

        var ownerClient = factory.CreateAuthenticatedClient(ownerId, org!.Id, MembershipRole.Owner);

        var propRes = await ownerClient.PostAsJsonAsync("/api/v1/properties", new
        {
            name = "Nile View",
            address = "10 Corniche",
            description = "Tower",
            type = PropertyType.Residential
        });
        Assert.Equal(HttpStatusCode.Created, propRes.StatusCode);
        var prop = await propRes.Content.ReadFromJsonAsync<PropertyResponse>();

        var unitRes = await ownerClient.PostAsJsonAsync($"/api/v1/properties/{prop!.Id}/units", new
        {
            unitNumber = $"U-{Guid.NewGuid():N}"[..8],
            floorNumber = 1,
            type = UnitType.Apartment,
            marketRent = rentAmount,
            bedrooms = 2,
            bathrooms = 1,
            areaSqFt = 110m
        });
        Assert.Equal(HttpStatusCode.Created, unitRes.StatusCode);
        var unit = await unitRes.Content.ReadFromJsonAsync<UnitResponse>();

        var residentRes = await ownerClient.PostAsJsonAsync("/api/v1/residents", new
        {
            firstName = "Omar",
            lastName = "Hassan",
            phone = "01012345678",
            email = $"omar-{Guid.NewGuid():N}@example.com"
        });
        Assert.Equal(HttpStatusCode.Created, residentRes.StatusCode);
        var resident = await residentRes.Content.ReadFromJsonAsync<ResidentResponse>();

        var leaseRes = await ownerClient.PostAsJsonAsync("/api/v1/leases", new
        {
            propertyId = prop.Id,
            unitId = unit!.Id,
            startDate = "2026-01-01",
            endDate = "2026-12-31",
            rentAmount,
            securityDeposit = rentAmount * 2,
            frequency = RentFrequency.Monthly,
            dueDayOfMonth = 5,
            gracePeriodDays = 5,
            lateFeePercentage = 5m,
            currency = defaultCurrency,
            primaryResidentId = resident!.Id
        });
        Assert.Equal(HttpStatusCode.Created, leaseRes.StatusCode);
        var lease = await leaseRes.Content.ReadFromJsonAsync<LeaseResponse>();

        Assert.Equal(HttpStatusCode.OK, (await ownerClient.PostAsJsonAsync($"/api/v1/leases/{lease!.Id}/submit", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await ownerClient.PostAsJsonAsync($"/api/v1/leases/{lease.Id}/approve", new { })).StatusCode);

        return (org.Id, resident.Id, lease);
    }

    private static async Task<InvoiceResponse> GenerateIssuedInvoiceAsync(
        HttpClient client,
        Guid leaseId,
        string periodStart,
        string periodEnd,
        string issueDate)
    {
        var res = await client.PostAsJsonAsync("/api/v1/billing/generate", new
        {
            leaseId,
            periodStart,
            periodEnd,
            issueDate,
            autoIssue = true
        });
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        return (await res.Content.ReadFromJsonAsync<InvoiceResponse>())!;
    }

    [Fact]
    public async Task FullPaymentAndLedgerWorkflow_ShouldCoverIntents_Allocations_Refunds_Reversal_AndBalancedLedger()
    {
        await using var factory = new TestApiFactory();
        var (orgId, residentId, lease) = await SeedActiveLeaseAsync(factory, "EGP", 1000m);
        using var ownerClient = factory.CreateAuthenticatedClient(Guid.NewGuid(), orgId, MembershipRole.Owner);

        // Issue two invoices (Jan = 1000, Feb = 1000) -> posts Debit A/R 2000, Credit RentalRevenue 2000
        var inv1 = await GenerateIssuedInvoiceAsync(ownerClient, lease.Id, "2026-01-01", "2026-01-31", "2026-01-01");
        var inv2 = await GenerateIssuedInvoiceAsync(ownerClient, lease.Id, "2026-02-01", "2026-02-28", "2026-02-01");

        // 1. Create & Get PaymentIntent
        var intentReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/payment-intents")
        {
            Content = JsonContent.Create(new
            {
                residentId,
                leaseId = lease.Id,
                targetInvoiceId = inv1.Id,
                amount = 600m,
                currency = "EGP",
                paymentMethod = PaymentMethod.Cash,
                provider = PaymentProviderType.Manual,
                description = "Jan partial cash intent"
            })
        };
        intentReq.Headers.Add("Idempotency-Key", "api-intent-1");
        var intentRes = await ownerClient.SendAsync(intentReq);
        Assert.Equal(HttpStatusCode.Created, intentRes.StatusCode);
        var intent = await intentRes.Content.ReadFromJsonAsync<PaymentIntentResponse>();
        Assert.NotNull(intent);

        var getIntentRes = await ownerClient.GetAsync($"/api/v1/payment-intents/{intent!.Id}");
        Assert.Equal(HttpStatusCode.OK, getIntentRes.StatusCode);

        // 2. Partial manual payment 1 (600 -> inv1) using Idempotency-Key header + retry check
        HttpRequestMessage BuildPay1()
        {
            var m = new HttpRequestMessage(HttpMethod.Post, "/api/v1/payments")
            {
                Content = JsonContent.Create(new
                {
                    residentId,
                    leaseId = lease.Id,
                    paymentIntentId = intent.Id,
                    amount = 600m,
                    currency = "EGP",
                    paymentMethod = PaymentMethod.Cash,
                    provider = PaymentProviderType.Manual,
                    allocationStrategy = AllocationStrategy.Explicit,
                    allocations = new[] { new { invoiceId = inv1.Id, amount = 600m } },
                    reference = "CASH-001"
                })
            };
            m.Headers.Add("Idempotency-Key", "api-pay-1");
            return m;
        }

        var pay1Res = await ownerClient.SendAsync(BuildPay1());
        Assert.Equal(HttpStatusCode.Created, pay1Res.StatusCode);
        var pay1 = await pay1Res.Content.ReadFromJsonAsync<PaymentResponse>();
        Assert.Equal(PaymentStatus.Succeeded, pay1!.Status);
        Assert.Equal(600m, pay1.AllocatedAmount);
        Assert.Equal(0m, pay1.UnappliedAmount);

        var pay1RetryRes = await ownerClient.SendAsync(BuildPay1());
        Assert.Equal(HttpStatusCode.Created, pay1RetryRes.StatusCode);
        var pay1Retry = await pay1RetryRes.Content.ReadFromJsonAsync<PaymentResponse>();
        Assert.Equal(pay1.Id, pay1Retry!.Id);

        // 3. Multi-invoice + Overpayment Payment 2 (2000 EGP -> 400 to inv1, 1000 to inv2, 600 unapplied)
        var pay2Res = await ownerClient.PostAsJsonAsync("/api/v1/payments", new
        {
            residentId,
            leaseId = lease.Id,
            amount = 2000m,
            currency = "EGP",
            paymentMethod = PaymentMethod.BankTransfer,
            provider = PaymentProviderType.Manual,
            allocationStrategy = AllocationStrategy.OldestDueFirst,
            overpaymentPolicy = OverpaymentPolicy.KeepUnappliedCredit,
            idempotencyKey = "api-pay-2"
        });
        Assert.Equal(HttpStatusCode.Created, pay2Res.StatusCode);
        var pay2 = await pay2Res.Content.ReadFromJsonAsync<PaymentResponse>();
        Assert.Equal(1400m, pay2!.AllocatedAmount);
        Assert.Equal(600m, pay2.UnappliedAmount);
        Assert.Equal(2, pay2.Allocations.Count);

        // Verify PagedList metadata accuracy (regression check for PageNumber/PageSize/TotalCount order)
        var pagedPaymentsRes = await ownerClient.GetAsync("/api/v1/payments?pageNumber=1&pageSize=1");
        Assert.Equal(HttpStatusCode.OK, pagedPaymentsRes.StatusCode);
        var pagedPayments = await pagedPaymentsRes.Content.ReadFromJsonAsync<PagedList<PaymentResponse>>();
        Assert.Equal(1, pagedPayments!.PageNumber);
        Assert.Equal(1, pagedPayments.PageSize);
        Assert.Equal(2, pagedPayments.TotalCount);
        Assert.Equal(2, pagedPayments.TotalPages);
        Assert.True(pagedPayments.HasNextPage);

        // 4. Over-refund blocked (> 600 unapplied)
        var overRefundRes = await ownerClient.PostAsJsonAsync($"/api/v1/payments/{pay2.Id}/refunds", new
        {
            amount = 700m,
            reason = "Too much",
            idempotencyKey = "api-ref-over"
        });
        Assert.Equal(HttpStatusCode.BadRequest, overRefundRes.StatusCode);

        // 5. Partial refund (400 from 600 unapplied)
        var refundRes = await ownerClient.PostAsJsonAsync($"/api/v1/payments/{pay2.Id}/refunds", new
        {
            amount = 400m,
            reason = "Return excess credit",
            idempotencyKey = "api-ref-1"
        });
        Assert.Equal(HttpStatusCode.Created, refundRes.StatusCode);
        var refund = await refundRes.Content.ReadFromJsonAsync<RefundResponse>();
        Assert.Equal(RefundStatus.Succeeded, refund!.Status);

        var listRefundsRes = await ownerClient.GetAsync($"/api/v1/payments/{pay2.Id}/refunds");
        Assert.Equal(HttpStatusCode.OK, listRefundsRes.StatusCode);
        var refunds = await listRefundsRes.Content.ReadFromJsonAsync<List<RefundResponse>>();
        Assert.Single(refunds!);

        // 6. Reverse inv2 allocation (1000 EGP) -> inv2 returns to Issued (BalanceDue = 1000)
        var inv2Alloc = pay2.Allocations.Single(a => a.InvoiceId == inv2.Id);
        var revRes = await ownerClient.PostAsJsonAsync(
            $"/api/v1/payments/{pay2.Id}/allocations/{inv2Alloc.Id}/reverse",
            new { reason = "Reassign to different period" });
        Assert.Equal(HttpStatusCode.OK, revRes.StatusCode);
        var afterRev = await revRes.Content.ReadFromJsonAsync<PaymentResponse>();
        Assert.Equal(400m, afterRev!.AllocatedAmount);
        Assert.Equal(1200m, afterRev.UnappliedAmount);

        var inv2AfterRev = await (await ownerClient.GetAsync($"/api/v1/invoices/{inv2.Id}")).Content.ReadFromJsonAsync<InvoiceResponse>();
        Assert.Equal(InvoiceStatus.Overdue, inv2AfterRev!.Status);
        Assert.Equal(1000m, inv2AfterRev.BalanceDue);

        // 7. Verify Ledger Balances (Trial Balance + A/R + RentalRevenue + Cash + UnappliedCredit)
        var balancesRes = await ownerClient.GetAsync("/api/v1/ledger/balances");
        Assert.Equal(HttpStatusCode.OK, balancesRes.StatusCode);
        var balances = await balancesRes.Content.ReadFromJsonAsync<List<AccountBalanceSummary>>();
        Assert.NotNull(balances);

        Assert.Equal(balances!.Sum(b => b.TotalDebits), balances.Sum(b => b.TotalCredits));
        Assert.Equal(2200m, balances.Single(b => b.AccountCode == LedgerAccountCode.Cash).NetBalance);
        Assert.Equal(1000m, balances.Single(b => b.AccountCode == LedgerAccountCode.AccountsReceivable).NetBalance);
        Assert.Equal(2000m, balances.Single(b => b.AccountCode == LedgerAccountCode.RentalRevenue).NetBalance);
        Assert.Equal(1200m, balances.Single(b => b.AccountCode == LedgerAccountCode.CustomerUnappliedCredit).NetBalance);

        // Verify Ledger Entries & Audit Logs pagination
        var entriesRes = await ownerClient.GetAsync("/api/v1/ledger/entries?pageNumber=1&pageSize=10");
        Assert.Equal(HttpStatusCode.OK, entriesRes.StatusCode);
        var entries = await entriesRes.Content.ReadFromJsonAsync<PagedList<JournalEntryResponse>>();
        Assert.Equal(1, entries!.PageNumber);
        Assert.Equal(10, entries.PageSize);
        Assert.Equal(6, entries.TotalCount); // 2 InvoiceIssued + 2 PaymentSucceeded + 1 Refund + 1 AllocationReversed

        var auditRes = await ownerClient.GetAsync("/api/v1/ledger/audit-logs?pageNumber=1&pageSize=20");
        Assert.Equal(HttpStatusCode.OK, auditRes.StatusCode);
        var audits = await auditRes.Content.ReadFromJsonAsync<PagedList<FinancialAuditLogResponse>>();
        Assert.True(audits!.TotalCount >= 6);
    }

    [Fact]
    public async Task WebhookSecurity_Settlement_AndReconciliation_ShouldWorkEndToEnd()
    {
        await using var factory = new TestApiFactory();
        var (orgId, residentId, lease) = await SeedActiveLeaseAsync(factory, "EGP", 2000m);
        using var ownerClient = factory.CreateAuthenticatedClient(Guid.NewGuid(), orgId, MembershipRole.Owner);
        using var anonClient = factory.CreateClient();

        await GenerateIssuedInvoiceAsync(ownerClient, lease.Id, "2026-03-01", "2026-03-31", "2026-03-01");

        // Create Simulated processing payment
        var payRes = await ownerClient.PostAsJsonAsync("/api/v1/payments", new
        {
            residentId,
            leaseId = lease.Id,
            amount = 2000m,
            currency = "EGP",
            paymentMethod = PaymentMethod.Card,
            provider = PaymentProviderType.Simulated,
            overpaymentPolicy = OverpaymentPolicy.AutoApplyToOldestInvoices,
            reference = "simulate:processing",
            idempotencyKey = "api-sim-1"
        });
        Assert.Equal(HttpStatusCode.Created, payRes.StatusCode);
        var payment = await payRes.Content.ReadFromJsonAsync<PaymentResponse>();
        Assert.Equal(PaymentStatus.Processing, payment!.Status);

        var payload = $$"""{"eventId":"evt_api_1","eventType":"payment.succeeded","providerPaymentId":"{{payment.ProviderPaymentId}}","providerTransactionId":"sim_tx_api_1","amount":2000,"feeAmount":52,"currency":"EGP"}""";

        // 1. Invalid signature -> 401
        var badSigReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/webhooks/payments/simulated")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        badSigReq.Headers.Add("X-Simulated-Signature", "t=1700000000,v1=deadbeef");
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonClient.SendAsync(badSigReq)).StatusCode);

        // 2. Expired timestamp (replay rejection) -> 400
        var oldTs = DateTimeOffset.UtcNow.AddMinutes(-20).ToUnixTimeSeconds();
        var oldSig = SimulatedPaymentProvider.ComputeWebhookSignature(SimWebhookSecret, oldTs, payload);
        var oldReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/webhooks/payments/simulated")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        oldReq.Headers.Add("X-Simulated-Signature", oldSig);
        Assert.Equal(HttpStatusCode.BadRequest, (await anonClient.SendAsync(oldReq)).StatusCode);

        // 3. Valid webhook -> 200 Processed
        var validTs = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var validSig = SimulatedPaymentProvider.ComputeWebhookSignature(SimWebhookSecret, validTs, payload);
        HttpRequestMessage BuildValidWebhook()
        {
            var r = new HttpRequestMessage(HttpMethod.Post, "/api/v1/webhooks/payments/simulated")
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };
            r.Headers.Add("X-Simulated-Signature", validSig);
            return r;
        }

        var okWhRes = await anonClient.SendAsync(BuildValidWebhook());
        Assert.Equal(HttpStatusCode.OK, okWhRes.StatusCode);
        var okWh = await okWhRes.Content.ReadFromJsonAsync<WebhookProcessingResponse>();
        Assert.Equal(WebhookEventStatus.Processed, okWh!.Status);
        Assert.False(okWh.IsDuplicate);

        // 4. Duplicate webhook -> 200 IgnoredDuplicate
        var dupWhRes = await anonClient.SendAsync(BuildValidWebhook());
        Assert.Equal(HttpStatusCode.OK, dupWhRes.StatusCode);
        var dupWh = await dupWhRes.Content.ReadFromJsonAsync<WebhookProcessingResponse>();
        Assert.Equal(WebhookEventStatus.IgnoredDuplicate, dupWh!.Status);
        Assert.True(dupWh.IsDuplicate);

        // 5. Settlement with fee -> Cash 1948 + FeeExpense 52 = Clearing 2000
        var settleRes = await ownerClient.PostAsJsonAsync($"/api/v1/payments/{payment.Id}/settle", new
        {
            providerFee = 52m,
            settledAtUtc = DateTime.UtcNow
        });
        Assert.Equal(HttpStatusCode.OK, settleRes.StatusCode);
        var settled = await settleRes.Content.ReadFromJsonAsync<PaymentResponse>();
        Assert.Equal(1948m, settled!.NetSettlementAmount);

        // 6. Reconciliation (1 matched + 1 extra unmatched provider tx -> resolve)
        var reconcileRes = await ownerClient.PostAsJsonAsync("/api/v1/payments/reconcile", new
        {
            provider = PaymentProviderType.Simulated,
            fromUtc = DateTime.UtcNow.AddHours(-1),
            toUtc = DateTime.UtcNow.AddHours(1),
            providerTransactionsOverride = new[]
            {
                new ProviderTransactionSnapshot("sim_tx_api_1", 2000m, 52m, 1948m, "EGP", payment.ProviderPaymentId, DateTime.UtcNow),
                new ProviderTransactionSnapshot("sim_tx_extra", 350m, 10m, 340m, "EGP", "sim_extra", DateTime.UtcNow)
            }
        });
        Assert.Equal(HttpStatusCode.OK, reconcileRes.StatusCode);
        var batch = await reconcileRes.Content.ReadFromJsonAsync<ReconciliationBatchResponse>();
        Assert.Equal(2, batch!.TotalChecked);
        Assert.Equal(1, batch.MatchedCount);
        Assert.Equal(1, batch.ExceptionCount);

        var recListRes = await ownerClient.GetAsync($"/api/v1/payments/reconciliation?batchId={batch.BatchId}&pageNumber=1&pageSize=10");
        Assert.Equal(HttpStatusCode.OK, recListRes.StatusCode);
        var recList = await recListRes.Content.ReadFromJsonAsync<PagedList<ReconciliationRecordResponse>>();
        Assert.Equal(2, recList!.TotalCount);

        var exceptionRecord = recList.Items.Single(r => r.Status == ReconciliationStatus.MissingInternalPayment);
        var resolveRes = await ownerClient.PostAsJsonAsync(
            $"/api/v1/payments/reconciliation/{exceptionRecord.Id}/resolve",
            new { resolutionNotes = "Verified orphaned test transaction" });
        Assert.Equal(HttpStatusCode.OK, resolveRes.StatusCode);
        var resolved = await resolveRes.Content.ReadFromJsonAsync<ReconciliationRecordResponse>();
        Assert.Equal(ReconciliationStatus.Resolved, resolved!.Status);
    }

    [Fact]
    public async Task RBAC_AndCrossTenantIsolation_ShouldBeEnforcedOnPaymentsAndLedger()
    {
        await using var factory = new TestApiFactory();
        var (orgA, residentA, leaseA) = await SeedActiveLeaseAsync(factory, "USD", 1200m);
        var (orgB, _, _) = await SeedActiveLeaseAsync(factory, "EUR", 900m);

        using var ownerA = factory.CreateAuthenticatedClient(Guid.NewGuid(), orgA, MembershipRole.Owner);
        using var managerA = factory.CreateAuthenticatedClient(Guid.NewGuid(), orgA, MembershipRole.PropertyManager);
        using var staffA = factory.CreateAuthenticatedClient(Guid.NewGuid(), orgA, MembershipRole.Staff);
        using var ownerB = factory.CreateAuthenticatedClient(Guid.NewGuid(), orgB, MembershipRole.Owner);

        // Staff cannot create payments or read ledger (403), CAN read payments (200)
        Assert.Equal(HttpStatusCode.Forbidden, (await staffA.PostAsJsonAsync("/api/v1/payments", new
        {
            residentId = residentA,
            leaseId = leaseA.Id,
            amount = 500m,
            currency = "USD",
            paymentMethod = PaymentMethod.Cash,
            provider = PaymentProviderType.Manual
        })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await staffA.GetAsync("/api/v1/payments")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await staffA.GetAsync("/api/v1/ledger/entries")).StatusCode);

        // PropertyManager CAN create payments & read ledger balances, CANNOT refund or read audit logs
        var pmPayRes = await managerA.PostAsJsonAsync("/api/v1/payments", new
        {
            residentId = residentA,
            leaseId = leaseA.Id,
            amount = 500m,
            currency = "USD",
            paymentMethod = PaymentMethod.Cash,
            provider = PaymentProviderType.Manual,
            idempotencyKey = "pm-pay-1"
        });
        Assert.Equal(HttpStatusCode.Created, pmPayRes.StatusCode);
        var paymentA = await pmPayRes.Content.ReadFromJsonAsync<PaymentResponse>();

        Assert.Equal(HttpStatusCode.OK, (await managerA.GetAsync("/api/v1/ledger/balances")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await managerA.PostAsJsonAsync($"/api/v1/payments/{paymentA!.Id}/refunds", new
        {
            amount = 100m,
            reason = "Not allowed for PM"
        })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await managerA.GetAsync("/api/v1/ledger/audit-logs")).StatusCode);

        // Cross-tenant Owner B cannot view or refund Org A payment (404)
        Assert.Equal(HttpStatusCode.NotFound, (await ownerB.GetAsync($"/api/v1/payments/{paymentA.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await ownerB.PostAsJsonAsync($"/api/v1/payments/{paymentA.Id}/refunds", new
        {
            amount = 100m,
            reason = "Cross-tenant"
        })).StatusCode);

        // Owner A CAN view audit logs (200)
        Assert.Equal(HttpStatusCode.OK, (await ownerA.GetAsync("/api/v1/ledger/audit-logs")).StatusCode);
    }
}
