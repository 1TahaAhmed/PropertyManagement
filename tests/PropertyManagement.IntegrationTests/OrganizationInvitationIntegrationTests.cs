using Microsoft.EntityFrameworkCore;
using PropertyManagement.Domain.Entities.Organizations;
using PropertyManagement.Domain.Enums;
using PropertyManagement.Infrastructure.Persistence;
using PropertyManagement.Infrastructure.Persistence.Repositories;
using Xunit;

namespace PropertyManagement.IntegrationTests;

public class OrganizationInvitationIntegrationTests
{
    private static ApplicationDbContext CreateDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        return new ApplicationDbContext(options, null);
    }

    [Fact]
    public async Task Repository_ShouldPersistAndQueryInvitationsAndMultiOrgMemberships()
    {
        var dbName = Guid.NewGuid().ToString();
        var utcNow = DateTime.UtcNow;
        var userId = Guid.NewGuid();

        var org1 = Organization.Create("Alpha Realty", utcNow);
        var org2 = Organization.Create("Beta Holdings", utcNow);
        var pendingInvite = Invitation.Create(org1.Id, "member@test.com", MembershipRole.Staff, "hash-1", utcNow.AddDays(7), utcNow);
        var revokedInvite = Invitation.Create(org1.Id, "other@test.com", MembershipRole.Admin, "hash-2", utcNow.AddDays(7), utcNow);
        revokedInvite.Revoke(utcNow.AddMinutes(1));

        await using (var writeCtx = CreateDbContext(dbName))
        {
            var repo = new OrganizationRepository(writeCtx);
            await repo.AddAsync(org1);
            await repo.AddAsync(org2);
            await repo.AddMemberAsync(OrganizationMember.Create(org1.Id, userId, MembershipRole.Owner, utcNow));
            await repo.AddMemberAsync(OrganizationMember.Create(org2.Id, userId, MembershipRole.PropertyManager, utcNow));
            await repo.AddInvitationAsync(pendingInvite);
            await repo.AddInvitationAsync(revokedInvite);
            await repo.SaveChangesAsync();
        }

        await using (var readCtx = CreateDbContext(dbName))
        {
            var repo = new OrganizationRepository(readCtx);

            var userOrgs = await repo.GetUserOrganizationsAsync(userId);
            Assert.Equal(2, userOrgs.Count);

            var foundPending = await repo.GetPendingInvitationByEmailAsync(org1.Id, "  MEMBER@TEST.COM ");
            Assert.NotNull(foundPending);
            Assert.Equal(pendingInvite.Id, foundPending!.Id);

            var pendingOnly = await repo.GetInvitationsByOrganizationAsync(org1.Id, InvitationStatus.Pending);
            Assert.Single(pendingOnly);

            var allInvites = await repo.GetInvitationsByOrganizationAsync(org1.Id);
            Assert.Equal(2, allInvites.Count);

            var byHash = await repo.GetInvitationByTokenHashAsync("hash-1");
            Assert.NotNull(byHash);
            Assert.Equal(org1.Id, byHash!.OrganizationId);
        }
    }
}
