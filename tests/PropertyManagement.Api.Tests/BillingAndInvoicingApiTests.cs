using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using PropertyManagement.Application.Abstractions.Authentication;
using PropertyManagement.Application.Features.Billing.Responses;
using PropertyManagement.Application.Features.Leases.Responses;
using PropertyManagement.Application.Features.Organizations.Responses;
using PropertyManagement.Application.Features.Properties.Response;
using PropertyManagement.Application.Features.Residents.Responses;
using PropertyManagement.Application.Features.Units.Response;
using PropertyManagement.Domain.Enums;
using PropertyManagement.Infrastructure.Persistence;
using Xunit;

namespace PropertyManagement.Api.Tests;

public sealed class BillingAndInvoicingApiTests
{
    private sealed class TestApiFactory : WebApplicationFactory<Program>
    {
        private readonly string _databaseName = $"PropertyManagement_BillingApi_{Guid.NewGuid():N}";

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("ConnectionStrings:MyConn", @"Server=(localdb)\mssqllocaldb;Database=TestIgnored;");
            builder.UseSetting("Jwt:Secret", "super-secret-integration-test-signing-key-at-least-32-chars");
            builder.UseSetting("Jwt:Issuer", "PropertyManagement");
            builder.UseSetting("Jwt:Audience", "PropertyManagement");
            builder.UseSetting("Jwt:ExpiryMinutes", "15");
            builder.UseSetting("Jwt:RefreshTokenExpiryDays", "7");

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

    private static async Task<(Guid OrgId, LeaseResponse Lease)> SeedActiveLeaseAsync(
        TestApiFactory factory,
        string defaultCurrency = "EGP",
        DateOnly? startDate = null,
        DateOnly? endDate = null,
        decimal rentAmount = 3100m,
        int dueDay = 5)
    {
        var ownerId = Guid.NewGuid();
        var bootstrapClient = factory.CreateAuthenticatedClient(ownerId, Guid.NewGuid(), MembershipRole.Owner);

        var orgRes = await bootstrapClient.PostAsJsonAsync("/api/v1/organizations", new
        {
            name = $"Org-{Guid.NewGuid():N}",
            defaultCurrency
        });
        Assert.Equal(HttpStatusCode.Created, orgRes.StatusCode);
        var org = await orgRes.Content.ReadFromJsonAsync<OrganizationResponse>();
        Assert.NotNull(org);
        Assert.Equal(defaultCurrency.ToUpperInvariant(), org!.DefaultCurrency);

        var ownerClient = factory.CreateAuthenticatedClient(ownerId, org.Id, MembershipRole.Owner);

        var propRes = await ownerClient.PostAsJsonAsync("/api/v1/properties", new
        {
            name = "Maadi Residences",
            address = "Road 9, Maadi",
            description = "Residential building",
            type = PropertyType.Residential
        });
        Assert.Equal(HttpStatusCode.Created, propRes.StatusCode);
        var property = await propRes.Content.ReadFromJsonAsync<PropertyResponse>();

        var unitRes = await ownerClient.PostAsJsonAsync($"/api/v1/properties/{property!.Id}/units", new
        {
            unitNumber = "202",
            floorNumber = 2,
            type = UnitType.Apartment,
            marketRent = rentAmount,
            bedrooms = 2,
            bathrooms = 1,
            squareMeters = 120m
        });
        Assert.Equal(HttpStatusCode.Created, unitRes.StatusCode);
        var unit = await unitRes.Content.ReadFromJsonAsync<UnitResponse>();

        var residentRes = await ownerClient.PostAsJsonAsync("/api/v1/residents", new
        {
            firstName = "Omar",
            lastName = "Nabil",
            phone = "+201022334455",
            email = "omar@example.com",
            nationalIdOrPassport = "29501011234567"
        });
        Assert.Equal(HttpStatusCode.Created, residentRes.StatusCode);
        var resident = await residentRes.Content.ReadFromJsonAsync<ResidentResponse>();

        var leaseStart = startDate ?? new DateOnly(2026, 3, 16);
        var leaseEnd = endDate ?? new DateOnly(2027, 3, 15);

        // Omit currency intentionally -> must inherit Organization.DefaultCurrency
        var leaseRes = await ownerClient.PostAsJsonAsync("/api/v1/leases", new
        {
            propertyId = property.Id,
            unitId = unit!.Id,
            startDate = leaseStart.ToString("yyyy-MM-dd"),
            endDate = leaseEnd.ToString("yyyy-MM-dd"),
            rentAmount,
            securityDeposit = rentAmount * 2,
            frequency = RentFrequency.Monthly,
            dueDayOfMonth = dueDay,
            gracePeriodDays = 5,
            lateFeePercentage = 5m,
            primaryResidentId = resident!.Id
        });
        Assert.Equal(HttpStatusCode.Created, leaseRes.StatusCode);
        var draftLease = await leaseRes.Content.ReadFromJsonAsync<LeaseResponse>();
        Assert.NotNull(draftLease);
        Assert.Equal(defaultCurrency.ToUpperInvariant(), draftLease!.Currency);

        var submitRes = await ownerClient.PostAsJsonAsync($"/api/v1/leases/{draftLease.Id}/submit", new { });
        Assert.Equal(HttpStatusCode.OK, submitRes.StatusCode);

        var approveRes = await ownerClient.PostAsJsonAsync($"/api/v1/leases/{draftLease.Id}/approve", new { });
        Assert.Equal(HttpStatusCode.OK, approveRes.StatusCode);
        var activeLease = await approveRes.Content.ReadFromJsonAsync<LeaseResponse>();

        return (org.Id, activeLease!);
    }

    [Fact]
    public async Task FullBillingWorkflow_ShouldPreview_GenerateDraft_PreventDuplicate_Issue_Adjust_MarkOverdue_AndVoid()
    {
        await using var factory = new TestApiFactory();
        var (orgId, lease) = await SeedActiveLeaseAsync(factory, defaultCurrency: "EGP", rentAmount: 3100m, dueDay: 5);

        using var managerClient = factory.CreateAuthenticatedClient(Guid.NewGuid(), orgId, MembershipRole.PropertyManager);
        using var ownerClient = factory.CreateAuthenticatedClient(Guid.NewGuid(), orgId, MembershipRole.Owner);

        // 1. Add two pending charges and void one
        var parkingRes = await managerClient.PostAsJsonAsync("/api/v1/billing/charges", new
        {
            leaseId = lease.Id,
            type = ChargeType.Parking,
            description = "Parking March",
            quantity = 1m,
            unitPrice = 300m,
            periodStart = "2026-03-16",
            periodEnd = "2026-03-31"
        });
        Assert.Equal(HttpStatusCode.Created, parkingRes.StatusCode);
        var parkingCharge = await parkingRes.Content.ReadFromJsonAsync<ChargeResponse>();
        Assert.Equal("EGP", parkingCharge!.Currency);

        var mistakeChargeRes = await managerClient.PostAsJsonAsync("/api/v1/billing/charges", new
        {
            leaseId = lease.Id,
            type = ChargeType.OtherFee,
            description = "Mistaken fee",
            quantity = 1m,
            unitPrice = 999m
        });
        Assert.Equal(HttpStatusCode.Created, mistakeChargeRes.StatusCode);
        var mistakeCharge = await mistakeChargeRes.Content.ReadFromJsonAsync<ChargeResponse>();

        var voidChargeRes = await managerClient.PostAsJsonAsync(
            $"/api/v1/billing/charges/{mistakeCharge!.Id}/void",
            new { reason = "Duplicate entry" });
        Assert.Equal(HttpStatusCode.OK, voidChargeRes.StatusCode);

        var pendingRes = await managerClient.GetAsync($"/api/v1/billing/leases/{lease.Id}/charges/pending");
        Assert.Equal(HttpStatusCode.OK, pendingRes.StatusCode);
        var pendingCharges = await pendingRes.Content.ReadFromJsonAsync<List<ChargeResponse>>();
        Assert.NotNull(pendingCharges);
        Assert.Single(pendingCharges!);
        Assert.Equal(parkingCharge.Id, pendingCharges[0].Id);

        // 2. Preview Billing (16/31 * 3100 = 1600 + 300 parking - 100 discount = 1800)
        var previewRes = await managerClient.PostAsJsonAsync("/api/v1/billing/preview", new
        {
            leaseId = lease.Id,
            periodStart = "2026-03-01",
            periodEnd = "2026-03-31",
            issueDate = "2026-03-16",
            discounts = new[] { new { description = "Move-in discount", amount = 100m } }
        });
        Assert.Equal(HttpStatusCode.OK, previewRes.StatusCode);
        var preview = await previewRes.Content.ReadFromJsonAsync<BillingPreviewResponse>();
        Assert.NotNull(preview);
        Assert.True(preview!.IsProrated);
        Assert.Equal(1800m, preview.Total);
        Assert.False(preview.AlreadyBilledForPeriod);

        // 3. Generate Invoice (defaults to Draft)
        var genRes = await managerClient.PostAsJsonAsync("/api/v1/billing/generate", new
        {
            leaseId = lease.Id,
            periodStart = "2026-03-01",
            periodEnd = "2026-03-31",
            issueDate = "2026-03-16",
            discounts = new[] { new { description = "Move-in discount", amount = 100m } }
        });
        Assert.Equal(HttpStatusCode.Created, genRes.StatusCode);
        var draftInvoice = await genRes.Content.ReadFromJsonAsync<InvoiceResponse>();
        Assert.NotNull(draftInvoice);
        Assert.Equal(InvoiceStatus.Draft, draftInvoice!.Status);
        Assert.Null(draftInvoice.InvoiceNumber);
        Assert.Equal(1800m, draftInvoice.Total);
        Assert.Equal("EGP", draftInvoice.Currency);

        // Verify pending charges are now empty because parking was billed
        var pendingAfterGen = await (await managerClient.GetAsync($"/api/v1/billing/leases/{lease.Id}/charges/pending"))
            .Content.ReadFromJsonAsync<List<ChargeResponse>>();
        Assert.Empty(pendingAfterGen!);

        // 4. Duplicate generation for same period returns 409 Conflict
        var dupRes = await managerClient.PostAsJsonAsync("/api/v1/billing/generate", new
        {
            leaseId = lease.Id,
            periodStart = "2026-03-01",
            periodEnd = "2026-03-31",
            issueDate = "2026-03-16"
        });
        Assert.Equal(HttpStatusCode.Conflict, dupRes.StatusCode);

        // 5. Issue Draft Invoice -> assigns INV-2026-000001
        var issueRes = await managerClient.PostAsJsonAsync($"/api/v1/invoices/{draftInvoice.Id}/issue", new
        {
            issueDate = "2026-03-16"
        });
        Assert.Equal(HttpStatusCode.OK, issueRes.StatusCode);
        var issuedInvoice = await issueRes.Content.ReadFromJsonAsync<InvoiceResponse>();
        Assert.Equal(InvoiceStatus.Issued, issuedInvoice!.Status);
        Assert.Equal("INV-2026-000001", issuedInvoice.InvoiceNumber);

        // 6. Apply LateFee adjustment (500) -> Total becomes 2300 without altering Subtotal (1800)
        var adjRes = await managerClient.PostAsJsonAsync($"/api/v1/invoices/{draftInvoice.Id}/adjustments", new
        {
            type = InvoiceAdjustmentType.Debit,
            chargeType = ChargeType.LateFee,
            amount = 500m,
            description = "Late fee",
            reason = "Past due date"
        });
        Assert.Equal(HttpStatusCode.OK, adjRes.StatusCode);
        var adjustedInvoice = await adjRes.Content.ReadFromJsonAsync<InvoiceResponse>();
        Assert.Equal(1800m, adjustedInvoice!.Subtotal);
        Assert.Equal(500m, adjustedInvoice.TotalAdjustments);
        Assert.Equal(2300m, adjustedInvoice.Total);
        Assert.Equal(2300m, adjustedInvoice.BalanceDue);

        // 7. Mark Overdue after DueDate (DueDate clamped to IssueDate 2026-03-16, so 2026-03-20 is overdue)
        var overdueRes = await managerClient.PostAsJsonAsync("/api/v1/invoices/mark-overdue", new
        {
            asOfDate = "2026-03-20"
        });
        Assert.Equal(HttpStatusCode.OK, overdueRes.StatusCode);
        var overdueSummary = await overdueRes.Content.ReadFromJsonAsync<MarkOverdueInvoicesResponse>();
        Assert.Equal(1, overdueSummary!.MarkedCount);
        Assert.Contains(draftInvoice.Id, overdueSummary.MarkedInvoiceIds);

        // Verify filtered GET /api/v1/invoices?onlyOverdue=true
        var listRes = await managerClient.GetAsync("/api/v1/invoices?onlyOverdue=true");
        Assert.Equal(HttpStatusCode.OK, listRes.StatusCode);
        var paged = await listRes.Content.ReadFromJsonAsync<PagedInvoiceResponse>();
        Assert.Single(paged!.Items);
        Assert.Equal(InvoiceStatus.Overdue, paged.Items[0].Status);

        // 8. PropertyManager cannot void invoice (403); Owner CAN void (200) and releases linked charge back to Pending
        var managerVoidRes = await managerClient.PostAsJsonAsync($"/api/v1/invoices/{draftInvoice.Id}/void", new
        {
            reason = "Manager attempt"
        });
        Assert.Equal(HttpStatusCode.Forbidden, managerVoidRes.StatusCode);

        var ownerVoidRes = await ownerClient.PostAsJsonAsync($"/api/v1/invoices/{draftInvoice.Id}/void", new
        {
            reason = "Re-issuing corrected invoice"
        });
        Assert.Equal(HttpStatusCode.OK, ownerVoidRes.StatusCode);
        var voided = await ownerVoidRes.Content.ReadFromJsonAsync<InvoiceResponse>();
        Assert.Equal(InvoiceStatus.Void, voided!.Status);
        Assert.Equal(0m, voided.BalanceDue);

        // Linked charge was released back to Pending and period can be re-generated cleanly
        var pendingAfterVoid = await (await ownerClient.GetAsync($"/api/v1/billing/leases/{lease.Id}/charges/pending"))
            .Content.ReadFromJsonAsync<List<ChargeResponse>>();
        Assert.Single(pendingAfterVoid!);
        Assert.Equal(parkingCharge.Id, pendingAfterVoid![0].Id);

        var regenRes = await ownerClient.PostAsJsonAsync("/api/v1/billing/generate", new
        {
            leaseId = lease.Id,
            periodStart = "2026-03-01",
            periodEnd = "2026-03-31",
            issueDate = "2026-03-16",
            autoIssue = true
        });
        Assert.Equal(HttpStatusCode.Created, regenRes.StatusCode);
        var reissued = await regenRes.Content.ReadFromJsonAsync<InvoiceResponse>();
        Assert.Equal(InvoiceStatus.Issued, reissued!.Status);
        Assert.Equal("INV-2026-000002", reissued.InvoiceNumber);
    }

    [Fact]
    public async Task ManualInvoice_RBAC_AndCrossTenantIsolation_ShouldBeEnforced()
    {
        await using var factory = new TestApiFactory();
        var (orgA, leaseA) = await SeedActiveLeaseAsync(factory, defaultCurrency: "USD", startDate: new DateOnly(2026, 1, 1), endDate: new DateOnly(2026, 12, 31), rentAmount: 10000m);
        var (orgB, _) = await SeedActiveLeaseAsync(factory, defaultCurrency: "EUR", startDate: new DateOnly(2026, 1, 1), endDate: new DateOnly(2026, 12, 31), rentAmount: 5000m);

        using var ownerAClient = factory.CreateAuthenticatedClient(Guid.NewGuid(), orgA, MembershipRole.Owner);
        using var staffAClient = factory.CreateAuthenticatedClient(Guid.NewGuid(), orgA, MembershipRole.Staff);
        using var ownerBClient = factory.CreateAuthenticatedClient(Guid.NewGuid(), orgB, MembershipRole.Owner);

        // Staff cannot create manual invoice or generate billing (403)
        var staffManualRes = await staffAClient.PostAsJsonAsync("/api/v1/invoices", new
        {
            leaseId = leaseA.Id,
            periodStart = "2026-04-01",
            periodEnd = "2026-04-30",
            dueDate = "2026-04-10",
            lines = new[] { new { type = ChargeType.OtherFee, description = "Key fob", quantity = 1m, unitPrice = 50m } }
        });
        Assert.Equal(HttpStatusCode.Forbidden, staffManualRes.StatusCode);

        // Owner A creates manual invoice with AutoIssue = true
        var ownerManualRes = await ownerAClient.PostAsJsonAsync("/api/v1/invoices", new
        {
            leaseId = leaseA.Id,
            periodStart = "2026-04-01",
            periodEnd = "2026-04-30",
            issueDate = "2026-04-01",
            dueDate = "2026-04-10",
            autoIssue = true,
            lines = new[] { new { type = ChargeType.OtherFee, description = "Key fob replacement", quantity = 2m, unitPrice = 75m } }
        });
        Assert.Equal(HttpStatusCode.Created, ownerManualRes.StatusCode);
        var manualInvoice = await ownerManualRes.Content.ReadFromJsonAsync<InvoiceResponse>();
        Assert.Equal(150m, manualInvoice!.Total);
        Assert.Equal("USD", manualInvoice.Currency);
        Assert.Equal("INV-2026-000001", manualInvoice.InvoiceNumber);

        // Staff CAN read invoices & preview
        var staffReadRes = await staffAClient.GetAsync($"/api/v1/invoices/{manualInvoice.Id}");
        Assert.Equal(HttpStatusCode.OK, staffReadRes.StatusCode);

        // Cross-tenant Owner B cannot read or void Org A's invoice (404 NotFound)
        var crossGetRes = await ownerBClient.GetAsync($"/api/v1/invoices/{manualInvoice.Id}");
        Assert.Equal(HttpStatusCode.NotFound, crossGetRes.StatusCode);

        var crossVoidRes = await ownerBClient.PostAsJsonAsync($"/api/v1/invoices/{manualInvoice.Id}/void", new { reason = "Cross-tenant attack" });
        Assert.Equal(HttpStatusCode.NotFound, crossVoidRes.StatusCode);
    }
}
