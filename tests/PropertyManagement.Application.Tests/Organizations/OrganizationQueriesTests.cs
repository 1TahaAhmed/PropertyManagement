using NSubstitute;
using PropertyManagement.Application.Abstractions.Identity;
using PropertyManagement.Application.Abstractions.Organizations;
using PropertyManagement.Application.Features.Organizations.GetInvitations;
using PropertyManagement.Application.Features.Organizations.GetMembers;
using PropertyManagement.Application.Features.Organizations.GetMyOrganizations;
using PropertyManagement.Domain.Entities.Organizations;
using PropertyManagement.Domain.Enums;
using Xunit;

namespace PropertyManagement.Application.Tests.Organizations;

public class OrganizationQueriesTests
{
    private static readonly DateTime FixedNow = new(2026, 11, 13, 12, 0, 0, DateTimeKind.Utc);
    private readonly IOrganizationRepository _organizationRepository = Substitute.For<IOrganizationRepository>();
    private readonly IIdentityService _identityService = Substitute.For<IIdentityService>();

    [Fact]
    public async Task GetMyOrganizations_ShouldMarkCurrentOrganization()
    {
        var userId = Guid.NewGuid();
        var orgA = Organization.Create("Company A", FixedNow);
        var orgB = Organization.Create("Company B", FixedNow);

        _organizationRepository.GetUserOrganizationsAsync(userId, Arg.Any<CancellationToken>())
            .Returns([(orgA, [MembershipRole.Owner]), (orgB, [MembershipRole.Admin])]);

        var handler = new GetMyOrganizationsQueryHandler(_organizationRepository);
        var result = await handler.Handle(new GetMyOrganizationsQuery(userId, orgB.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Count);
        Assert.False(result.Value.Single(o => o.OrganizationId == orgA.Id).IsCurrent);
        Assert.True(result.Value.Single(o => o.OrganizationId == orgB.Id).IsCurrent);
    }

    [Fact]
    public async Task GetMembers_ShouldEnrichMembersWithIdentityDetails()
    {
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var member = OrganizationMember.Create(orgId, userId, MembershipRole.PropertyManager, FixedNow);

        _organizationRepository.GetMembersAsync(orgId, Arg.Any<CancellationToken>()).Returns([member]);
        _identityService.GetUsersByIdsAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, AuthUserResult>
            {
                [userId] = new AuthUserResult(userId, "pm@example.com", "Sara", "Ali")
            });

        var handler = new GetOrganizationMembersQueryHandler(_organizationRepository, _identityService);
        var result = await handler.Handle(new GetOrganizationMembersQuery(orgId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var item = Assert.Single(result.Value);
        Assert.Equal("pm@example.com", item.Email);
        Assert.Equal("Sara", item.FirstName);
        Assert.Equal("Ali", item.LastName);
        Assert.Equal(MembershipRole.PropertyManager, item.Role);
    }

    [Fact]
    public async Task GetInvitations_ShouldReturnMappedInvitations()
    {
        var orgId = Guid.NewGuid();
        var invite = Invitation.Create(orgId, "staff@example.com", MembershipRole.Staff, "hash", FixedNow.AddDays(7), FixedNow);

        _organizationRepository.GetInvitationsByOrganizationAsync(orgId, InvitationStatus.Pending, Arg.Any<CancellationToken>())
            .Returns([invite]);

        var handler = new GetOrganizationInvitationsQueryHandler(_organizationRepository);
        var result = await handler.Handle(new GetOrganizationInvitationsQuery(orgId, InvitationStatus.Pending), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var item = Assert.Single(result.Value);
        Assert.Equal("staff@example.com", item.Email);
        Assert.Equal(InvitationStatus.Pending, item.Status);
    }
}
