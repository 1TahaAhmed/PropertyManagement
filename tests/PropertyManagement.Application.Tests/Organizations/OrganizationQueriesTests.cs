using NSubstitute;
using PropertyManagement.Application.Abstractions;
using PropertyManagement.Application.Abstractions.Identity;
using PropertyManagement.Application.Abstractions.Organizations;
using PropertyManagement.Application.Common.Enums;
using PropertyManagement.Application.Features.Organizations.Create;
using PropertyManagement.Application.Features.Organizations.GetById;
using PropertyManagement.Application.Features.Organizations.GetMembers;
using PropertyManagement.Application.Features.Organizations.GetMyOrganizations;
using PropertyManagement.Application.Features.Organizations.Members;
using PropertyManagement.Application.Features.Organizations.Update;
using PropertyManagement.Domain.Entities.Organizations;
using PropertyManagement.Domain.Enums;
using Xunit;

namespace PropertyManagement.Application.Tests.Organizations;

public class OrganizationQueriesTests
{
    private static readonly DateTime FixedNow = new(2026, 11, 13, 12, 0, 0, DateTimeKind.Utc);
    private readonly IOrganizationRepository _organizationRepository = Substitute.For<IOrganizationRepository>();
    private readonly IIdentityService _identityService = Substitute.For<IIdentityService>();
    private readonly IClock _clock = Substitute.For<IClock>();

    public OrganizationQueriesTests()
    {
        _clock.UtcNow.Returns(FixedNow);
    }

    [Fact]
    public async Task CreateOrganization_ShouldCreateOrgAndOwnerMembership()
    {
        var userId = Guid.NewGuid();
        _organizationRepository.ExistsByNameAsync("Delta Group", Arg.Any<CancellationToken>()).Returns(false);
        _organizationRepository.ExistsByIdentifierAsync("delta-group", Arg.Any<CancellationToken>()).Returns(false);

        var handler = new CreateOrganizationCommandHandler(_organizationRepository, _clock);
        var result = await handler.Handle(new CreateOrganizationCommand(userId, "Delta Group"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Delta Group", result.Value.Name);
        Assert.Equal("delta-group", result.Value.Identifier);
        await _organizationRepository.Received(1).AddAsync(Arg.Any<Organization>(), Arg.Any<CancellationToken>());
        await _organizationRepository.Received(1).AddMemberAsync(
            Arg.Is<OrganizationMember>(m => m.UserId == userId && m.Role == MembershipRole.Owner),
            Arg.Any<CancellationToken>());
        await _organizationRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetOrganizationById_ShouldReturnForbidden_WhenUserIsNotActiveMember()
    {
        var org = Organization.Create("Delta Group", FixedNow);
        var userId = Guid.NewGuid();

        _organizationRepository.GetByIdAsync(org.Id, Arg.Any<CancellationToken>()).Returns(org);
        _organizationRepository.GetMemberAsync(org.Id, userId, Arg.Any<CancellationToken>()).Returns((OrganizationMember?)null);

        var handler = new GetOrganizationByIdQueryHandler(_organizationRepository);
        var result = await handler.Handle(new GetOrganizationByIdQuery(org.Id, userId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Forbidden, result.Errors.First().Type);
    }

    [Fact]
    public async Task UpdateOrganization_ShouldUpdateName_WhenUnique()
    {
        var org = Organization.Create("Old Name", FixedNow.AddDays(-1));
        _organizationRepository.GetByIdAsync(org.Id, Arg.Any<CancellationToken>()).Returns(org);
        _organizationRepository.ExistsByNameAsync("New Name", Arg.Any<CancellationToken>()).Returns(false);

        var handler = new UpdateOrganizationCommandHandler(_organizationRepository, _clock);
        var result = await handler.Handle(new UpdateOrganizationCommand(org.Id, "New Name"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("New Name", result.Value.Name);
        await _organizationRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeactivateMember_ShouldFail_WhenMemberIsLastActiveOwner()
    {
        var orgId = Guid.NewGuid();
        var member = OrganizationMember.Create(orgId, Guid.NewGuid(), MembershipRole.Owner, FixedNow);

        _organizationRepository.GetMemberByIdAsync(member.Id, orgId, Arg.Any<CancellationToken>()).Returns(member);
        _organizationRepository.CountActiveOwnersAsync(orgId, Arg.Any<CancellationToken>()).Returns(1);

        var handler = new DeactivateMemberCommandHandler(_organizationRepository, _clock);
        var result = await handler.Handle(new DeactivateMemberCommand(orgId, member.Id, Guid.NewGuid(), MembershipRole.Owner), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Organization.LastOwnerProtected", result.Errors.First().Code);
    }

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
}
