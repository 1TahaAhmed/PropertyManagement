using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PropertyManagement.Application.Abstractions.Authentication;
using PropertyManagement.Domain.Entities.Organizations;
using PropertyManagement.Domain.Enums;
using PropertyManagement.Infrastructure.Persistence;
using Xunit;

namespace PropertyManagement.Api.Tests;

public class OrganizationsMemberPrivilegeEscalationTests
{
    private static readonly DateTime FixedNow = new(2026, 11, 14, 12, 0, 0, DateTimeKind.Utc);

    private sealed class TestApiFactory : WebApplicationFactory<Program>
    {
        private readonly string _databaseName = $"PropertyManagement_ApiTests_{Guid.NewGuid():N}";

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("ConnectionStrings:MyConn", "Server=(localdb)\\mssqllocaldb;Database=TestIgnored;");
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
                    options.UseInMemoryDatabase(_databaseName));
            });
        }

        public HttpClient CreateAuthenticatedClient(
            Guid userId,
            Guid organizationId,
            MembershipRole role,
            string email = "actor@example.com")
        {
            using var scope = Services.CreateScope();
            var jwtGenerator = scope.ServiceProvider.GetRequiredService<IJwtTokenGenerator>();
            var (token, _) = jwtGenerator.GenerateAccessToken(
                userId: userId,
                email: email,
                firstName: "Test",
                lastName: "Actor",
                organizationId: organizationId,
                role: role.ToString());

            var client = CreateClient(new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false
            });
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return client;
        }

        public async Task<(Organization Org, OrganizationMember Owner1, OrganizationMember Owner2, OrganizationMember Admin1, OrganizationMember Admin2, OrganizationMember Pm, OrganizationMember Staff)> SeedMembersAsync()
        {
            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var org = Organization.Create($"Org-{Guid.NewGuid():N}"[..16], FixedNow);
            var owner1 = OrganizationMember.Create(org.Id, Guid.NewGuid(), MembershipRole.Owner, FixedNow);
            var owner2 = OrganizationMember.Create(org.Id, Guid.NewGuid(), MembershipRole.Owner, FixedNow);
            var admin1 = OrganizationMember.Create(org.Id, Guid.NewGuid(), MembershipRole.Admin, FixedNow);
            var admin2 = OrganizationMember.Create(org.Id, Guid.NewGuid(), MembershipRole.Admin, FixedNow);
            var pm = OrganizationMember.Create(org.Id, Guid.NewGuid(), MembershipRole.PropertyManager, FixedNow);
            var staff = OrganizationMember.Create(org.Id, Guid.NewGuid(), MembershipRole.Staff, FixedNow);

            await db.Organizations.AddAsync(org);
            await db.OrganizationMembers.AddRangeAsync(owner1, owner2, admin1, admin2, pm, staff);
            await db.SaveChangesAsync();

            return (org, owner1, owner2, admin1, admin2, pm, staff);
        }

        public async Task<OrganizationMember?> GetMemberFromDbAsync(Guid memberId)
        {
            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            return await db.OrganizationMembers.AsNoTracking().FirstOrDefaultAsync(m => m.Id == memberId);
        }
    }

    [Fact]
    public async Task Admin_WithRealJwt_CannotPromoteSelfToOwner_OrChangeOwnRole_OrDeactivateSelf()
    {
        await using var factory = new TestApiFactory();
        var (org, _, _, admin1, _, _, _) = await factory.SeedMembersAsync();
        using var adminClient = factory.CreateAuthenticatedClient(admin1.UserId, org.Id, MembershipRole.Admin);

        // Self-escalation to Owner -> 403
        var promoteSelfResponse = await adminClient.PutAsJsonAsync(
            $"/api/v1/organizations/members/{admin1.Id}/role",
            new { role = MembershipRole.Owner });
        Assert.Equal(HttpStatusCode.Forbidden, promoteSelfResponse.StatusCode);

        // Self-demotion to Staff -> 403
        var demoteSelfResponse = await adminClient.PutAsJsonAsync(
            $"/api/v1/organizations/members/{admin1.Id}/role",
            new { role = MembershipRole.Staff });
        Assert.Equal(HttpStatusCode.Forbidden, demoteSelfResponse.StatusCode);

        // Self-deactivation -> 403
        var deactivateSelfResponse = await adminClient.PostAsync(
            $"/api/v1/organizations/members/{admin1.Id}/deactivate",
            content: null);
        Assert.Equal(HttpStatusCode.Forbidden, deactivateSelfResponse.StatusCode);

        var persisted = await factory.GetMemberFromDbAsync(admin1.Id);
        Assert.NotNull(persisted);
        Assert.Equal(MembershipRole.Admin, persisted.Role);
        Assert.Equal(MembershipStatus.Active, persisted.Status);
    }

    [Fact]
    public async Task Admin_WithRealJwt_CannotPromoteAnotherMemberToOwner()
    {
        await using var factory = new TestApiFactory();
        var (org, _, _, admin1, _, _, staff) = await factory.SeedMembersAsync();
        using var adminClient = factory.CreateAuthenticatedClient(admin1.UserId, org.Id, MembershipRole.Admin);

        var response = await adminClient.PutAsJsonAsync(
            $"/api/v1/organizations/members/{staff.Id}/role",
            new { role = MembershipRole.Owner });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        var persistedStaff = await factory.GetMemberFromDbAsync(staff.Id);
        Assert.NotNull(persistedStaff);
        Assert.Equal(MembershipRole.Staff, persistedStaff.Role);
    }

    [Fact]
    public async Task Admin_WithRealJwt_CannotDemoteOrDeactivateOwner_EvenWhenTwoOwnersExist()
    {
        await using var factory = new TestApiFactory();
        var (org, _, owner2, admin1, _, _, _) = await factory.SeedMembersAsync();
        using var adminClient = factory.CreateAuthenticatedClient(admin1.UserId, org.Id, MembershipRole.Admin);

        var demoteOwnerResponse = await adminClient.PutAsJsonAsync(
            $"/api/v1/organizations/members/{owner2.Id}/role",
            new { role = MembershipRole.Staff });
        Assert.Equal(HttpStatusCode.Forbidden, demoteOwnerResponse.StatusCode);

        var deactivateOwnerResponse = await adminClient.PostAsync(
            $"/api/v1/organizations/members/{owner2.Id}/deactivate",
            content: null);
        Assert.Equal(HttpStatusCode.Forbidden, deactivateOwnerResponse.StatusCode);

        var persistedOwner2 = await factory.GetMemberFromDbAsync(owner2.Id);
        Assert.NotNull(persistedOwner2);
        Assert.Equal(MembershipRole.Owner, persistedOwner2.Role);
        Assert.Equal(MembershipStatus.Active, persistedOwner2.Status);
    }

    [Fact]
    public async Task Admin_WithRealJwt_CannotModifyOrDeactivateAnotherAdmin()
    {
        await using var factory = new TestApiFactory();
        var (org, _, _, admin1, admin2, _, _) = await factory.SeedMembersAsync();
        using var adminClient = factory.CreateAuthenticatedClient(admin1.UserId, org.Id, MembershipRole.Admin);

        var demoteAdminResponse = await adminClient.PutAsJsonAsync(
            $"/api/v1/organizations/members/{admin2.Id}/role",
            new { role = MembershipRole.PropertyManager });
        Assert.Equal(HttpStatusCode.Forbidden, demoteAdminResponse.StatusCode);

        var deactivateAdminResponse = await adminClient.PostAsync(
            $"/api/v1/organizations/members/{admin2.Id}/deactivate",
            content: null);
        Assert.Equal(HttpStatusCode.Forbidden, deactivateAdminResponse.StatusCode);

        var persistedAdmin2 = await factory.GetMemberFromDbAsync(admin2.Id);
        Assert.NotNull(persistedAdmin2);
        Assert.Equal(MembershipRole.Admin, persistedAdmin2.Role);
        Assert.Equal(MembershipStatus.Active, persistedAdmin2.Status);
    }

    [Fact]
    public async Task Admin_WithRealJwt_CanModifyAndDeactivatePropertyManagerAndStaff()
    {
        await using var factory = new TestApiFactory();
        var (org, _, _, admin1, _, pm, staff) = await factory.SeedMembersAsync();
        using var adminClient = factory.CreateAuthenticatedClient(admin1.UserId, org.Id, MembershipRole.Admin);

        var promoteStaffResponse = await adminClient.PutAsJsonAsync(
            $"/api/v1/organizations/members/{staff.Id}/role",
            new { role = MembershipRole.PropertyManager });
        Assert.Equal(HttpStatusCode.OK, promoteStaffResponse.StatusCode);

        var deactivatePmResponse = await adminClient.PostAsync(
            $"/api/v1/organizations/members/{pm.Id}/deactivate",
            content: null);
        Assert.Equal(HttpStatusCode.OK, deactivatePmResponse.StatusCode);

        var persistedStaff = await factory.GetMemberFromDbAsync(staff.Id);
        var persistedPm = await factory.GetMemberFromDbAsync(pm.Id);
        Assert.Equal(MembershipRole.PropertyManager, persistedStaff!.Role);
        Assert.Equal(MembershipStatus.Inactive, persistedPm!.Status);
    }

    [Fact]
    public async Task Owner_WithRealJwt_CanGrantAndRevokeOwner_AndModifyAdmin_ButCannotModifySelf()
    {
        await using var factory = new TestApiFactory();
        var (org, owner1, owner2, admin1, _, _, staff) = await factory.SeedMembersAsync();
        using var ownerClient = factory.CreateAuthenticatedClient(owner1.UserId, org.Id, MembershipRole.Owner);

        // Grant Owner to Staff -> 200
        var grantOwnerResponse = await ownerClient.PutAsJsonAsync(
            $"/api/v1/organizations/members/{staff.Id}/role",
            new { role = MembershipRole.Owner });
        Assert.Equal(HttpStatusCode.OK, grantOwnerResponse.StatusCode);

        // Revoke Owner from Owner2 -> 200
        var revokeOwnerResponse = await ownerClient.PutAsJsonAsync(
            $"/api/v1/organizations/members/{owner2.Id}/role",
            new { role = MembershipRole.Admin });
        Assert.Equal(HttpStatusCode.OK, revokeOwnerResponse.StatusCode);

        // Deactivate Admin1 -> 200
        var deactivateAdminResponse = await ownerClient.PostAsync(
            $"/api/v1/organizations/members/{admin1.Id}/deactivate",
            content: null);
        Assert.Equal(HttpStatusCode.OK, deactivateAdminResponse.StatusCode);

        // Self modification blocked -> 403
        var selfRoleResponse = await ownerClient.PutAsJsonAsync(
            $"/api/v1/organizations/members/{owner1.Id}/role",
            new { role = MembershipRole.Admin });
        Assert.Equal(HttpStatusCode.Forbidden, selfRoleResponse.StatusCode);

        var selfDeactivateResponse = await ownerClient.PostAsync(
            $"/api/v1/organizations/members/{owner1.Id}/deactivate",
            content: null);
        Assert.Equal(HttpStatusCode.Forbidden, selfDeactivateResponse.StatusCode);

        Assert.Equal(MembershipRole.Owner, (await factory.GetMemberFromDbAsync(staff.Id))!.Role);
        Assert.Equal(MembershipRole.Admin, (await factory.GetMemberFromDbAsync(owner2.Id))!.Role);
        Assert.Equal(MembershipStatus.Inactive, (await factory.GetMemberFromDbAsync(admin1.Id))!.Status);
        Assert.Equal(MembershipRole.Owner, (await factory.GetMemberFromDbAsync(owner1.Id))!.Role);
        Assert.Equal(MembershipStatus.Active, (await factory.GetMemberFromDbAsync(owner1.Id))!.Status);
    }

    [Fact]
    public async Task Staff_WithRealJwt_IsBlockedByManageMembersPolicy()
    {
        await using var factory = new TestApiFactory();
        var (org, _, _, _, _, pm, staff) = await factory.SeedMembersAsync();
        using var staffClient = factory.CreateAuthenticatedClient(staff.UserId, org.Id, MembershipRole.Staff);

        var response = await staffClient.PutAsJsonAsync(
            $"/api/v1/organizations/members/{pm.Id}/role",
            new { role = MembershipRole.Owner });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
