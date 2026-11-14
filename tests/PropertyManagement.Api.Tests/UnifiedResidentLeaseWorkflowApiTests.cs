using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using PropertyManagement.Application.Abstractions.Authentication;
using PropertyManagement.Application.Common.Models;
using PropertyManagement.Application.Features.Leases.Responses;
using PropertyManagement.Application.Features.Properties.Response;
using PropertyManagement.Application.Features.Residents.Responses;
using PropertyManagement.Application.Features.Units.Response;
using PropertyManagement.Domain.Enums;
using PropertyManagement.Infrastructure.Persistence;
using Xunit;

namespace PropertyManagement.Api.Tests;

public sealed class UnifiedResidentLeaseWorkflowApiTests
{
    private sealed class TestApiFactory : WebApplicationFactory<Program>
    {
        private readonly string _databaseName = $"PropertyManagement_UnifiedApi_{Guid.NewGuid():N}";

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
                "owner@test.com",
                "Test",
                "Owner",
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

    [Fact]
    public async Task HealthEndpoint_ShouldReturnOkAnonymously()
    {
        await using var factory = new TestApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task UnifiedResidentAndLeaseWorkflow_ShouldSupportTenantsView_BlacklistBlocking_AndDraftLifecycle()
    {
        await using var factory = new TestApiFactory();
        var orgId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        using var ownerClient = factory.CreateAuthenticatedClient(ownerId, orgId, MembershipRole.Owner);
        using var staffClient = factory.CreateAuthenticatedClient(Guid.NewGuid(), orgId, MembershipRole.Staff);

        // 1. Create Property & Unit
        var propRes = await ownerClient.PostAsJsonAsync("/api/v1/properties", new
        {
            name = "Zamalek Residence",
            address = "26th of July Corridor",
            description = "Luxury building",
            type = PropertyType.Residential
        });
        Assert.Equal(HttpStatusCode.Created, propRes.StatusCode);
        var property = await propRes.Content.ReadFromJsonAsync<PropertyResponse>();
        Assert.NotNull(property);

        var unitRes = await ownerClient.PostAsJsonAsync($"/api/v1/properties/{property!.Id}/units", new
        {
            unitNumber = "301",
            floorNumber = 3,
            type = UnitType.Apartment,
            marketRent = 20000m,
            bedrooms = 3,
            bathrooms = 2,
            squareMeters = 160m,
            description = "Corner unit"
        });
        Assert.Equal(HttpStatusCode.Created, unitRes.StatusCode);
        var unit = await unitRes.Content.ReadFromJsonAsync<UnitResponse>();
        Assert.NotNull(unit);

        // 2. Create Primary Tenant Resident, Occupant Resident, and Prospect Resident
        var primaryRes = await ownerClient.PostAsJsonAsync("/api/v1/residents", new
        {
            firstName = "Hossam",
            lastName = "Fathy",
            phone = "+201001112233",
            email = "hossam@example.com",
            nationalIdOrPassport = "29403031234567"
        });
        Assert.Equal(HttpStatusCode.Created, primaryRes.StatusCode);
        var primary = await primaryRes.Content.ReadFromJsonAsync<ResidentResponse>();
        Assert.NotNull(primary);
        Assert.Equal("29403031234567", primary!.NationalIdOrPassport);

        var occRes = await ownerClient.PostAsJsonAsync("/api/v1/residents", new
        {
            firstName = "Salma",
            lastName = "Fathy",
            phone = "+201001112244"
        });
        Assert.Equal(HttpStatusCode.Created, occRes.StatusCode);
        var occupant = await occRes.Content.ReadFromJsonAsync<ResidentResponse>();
        Assert.NotNull(occupant);

        var badRes = await ownerClient.PostAsJsonAsync("/api/v1/residents", new
        {
            firstName = "Banned",
            lastName = "Person",
            phone = "+201009998877"
        });
        var banned = await badRes.Content.ReadFromJsonAsync<ResidentResponse>();
        Assert.NotNull(banned);

        // Staff cannot blacklist
        var staffBlacklist = await staffClient.PostAsJsonAsync($"/api/v1/residents/{banned!.Id}/blacklist", new { reason = "Test" });
        Assert.Equal(HttpStatusCode.Forbidden, staffBlacklist.StatusCode);

        // Owner blacklists
        var ownerBlacklist = await ownerClient.PostAsJsonAsync($"/api/v1/residents/{banned.Id}/blacklist", new { reason = "Unpaid rent" });
        Assert.Equal(HttpStatusCode.OK, ownerBlacklist.StatusCode);

        // 3. Create Draft Lease & Update Draft Terms
        var createLeaseRes = await ownerClient.PostAsJsonAsync("/api/v1/leases", new
        {
            propertyId = property.Id,
            unitId = unit!.Id,
            startDate = "2027-01-01",
            endDate = "2027-12-31",
            rentAmount = 20000m,
            securityDeposit = 40000m,
            primaryResidentId = primary.Id
        });
        Assert.Equal(HttpStatusCode.Created, createLeaseRes.StatusCode);
        var lease = await createLeaseRes.Content.ReadFromJsonAsync<LeaseResponse>();
        Assert.NotNull(lease);

        var updateLeaseRes = await ownerClient.PutAsJsonAsync($"/api/v1/leases/{lease!.Id}", new
        {
            startDate = "2027-01-01",
            endDate = "2027-12-31",
            rentAmount = 22000m,
            securityDeposit = 44000m,
            frequency = RentFrequency.Monthly,
            dueDayOfMonth = 5,
            gracePeriodDays = 3,
            lateFeePercentage = 2m
        });
        Assert.Equal(HttpStatusCode.OK, updateLeaseRes.StatusCode);
        var updatedLease = await updateLeaseRes.Content.ReadFromJsonAsync<LeaseResponse>();
        Assert.Equal(22000m, updatedLease!.RentAmount);

        // 4. Add occupant, promote to CoTenant, and verify blacklisted resident is rejected
        var addOccRes = await ownerClient.PostAsJsonAsync($"/api/v1/leases/{lease.Id}/residents", new
        {
            residentId = occupant!.Id,
            role = LeaseResidentRole.Occupant
        });
        Assert.Equal(HttpStatusCode.OK, addOccRes.StatusCode);

        var roleUpdateRes = await ownerClient.PutAsJsonAsync(
            $"/api/v1/leases/{lease.Id}/residents/{occupant.Id}/role",
            new { role = LeaseResidentRole.CoTenant });
        Assert.Equal(HttpStatusCode.OK, roleUpdateRes.StatusCode);
        var roleUpdatedLease = await roleUpdateRes.Content.ReadFromJsonAsync<LeaseResponse>();
        Assert.True(roleUpdatedLease!.Residents.Single(r => r.ResidentId == occupant.Id).IsFinanciallyResponsible);

        var addBannedRes = await ownerClient.PostAsJsonAsync($"/api/v1/leases/{lease.Id}/residents", new
        {
            residentId = banned.Id,
            role = LeaseResidentRole.CoTenant
        });
        Assert.Equal(HttpStatusCode.BadRequest, addBannedRes.StatusCode);

        // 5. Remove CoTenant, leaving only PrimaryTenant
        var removeRes = await ownerClient.DeleteAsync($"/api/v1/leases/{lease.Id}/residents/{occupant.Id}");
        Assert.Equal(HttpStatusCode.OK, removeRes.StatusCode);

        // 6. Verify /api/v1/tenants returns ONLY the Lease-Holder (PrimaryTenant), NOT occupant or banned
        var tenantsRes = await ownerClient.GetAsync("/api/v1/tenants");
        Assert.Equal(HttpStatusCode.OK, tenantsRes.StatusCode);
        var tenantsPage = await tenantsRes.Content.ReadFromJsonAsync<PagedList<ResidentSummaryResponse>>();
        Assert.NotNull(tenantsPage);
        Assert.Single(tenantsPage!.Items);
        Assert.Equal(primary.Id, tenantsPage.Items[0].Id);
    }
}
