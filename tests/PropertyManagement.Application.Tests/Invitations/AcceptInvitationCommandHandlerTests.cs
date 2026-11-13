using NSubstitute;
using PropertyManagement.Application.Abstractions;
using PropertyManagement.Application.Abstractions.Authentication;
using PropertyManagement.Application.Abstractions.Identity;
using PropertyManagement.Application.Abstractions.Organizations;
using PropertyManagement.Application.Common.Enums;
using PropertyManagement.Application.Features.Invitations.Accept;
using PropertyManagement.Domain.Entities.Organizations;
using PropertyManagement.Domain.Enums;
using Xunit;

namespace PropertyManagement.Application.Tests.Invitations;

public class AcceptInvitationCommandHandlerTests
{
    private static readonly DateTime FixedNow = new(2026, 11, 13, 12, 0, 0, DateTimeKind.Utc);
    private readonly IOrganizationRepository _organizationRepository = Substitute.For<IOrganizationRepository>();
    private readonly IIdentityService _identityService = Substitute.For<IIdentityService>();
    private readonly IJwtTokenGenerator _jwtTokenGenerator = Substitute.For<IJwtTokenGenerator>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly AcceptInvitationCommandHandler _handler;

    public AcceptInvitationCommandHandlerTests()
    {
        _clock.UtcNow.Returns(FixedNow);
        _handler = new AcceptInvitationCommandHandler(
            _organizationRepository,
            _identityService,
            _jwtTokenGenerator,
            _clock);
    }

    [Fact]
    public async Task Handle_ShouldAddMemberAndAcceptInvitation_WhenTokenAndEmailMatch()
    {
        var org = Organization.Create("Nile Properties", FixedNow.AddDays(-10));
        var userId = Guid.NewGuid();
        var invitation = Invitation.Create(
            org.Id,
            "invited@example.com",
            MembershipRole.Admin,
            "hashed-token",
            FixedNow.AddDays(5),
            FixedNow.AddDays(-1));

        _jwtTokenGenerator.HashToken("plain-token").Returns("hashed-token");
        _organizationRepository.GetInvitationByTokenHashAsync("hashed-token", Arg.Any<CancellationToken>())
            .Returns(invitation);
        _organizationRepository.GetByIdAsync(org.Id, Arg.Any<CancellationToken>()).Returns(org);
        _organizationRepository.GetMemberAsync(org.Id, userId, Arg.Any<CancellationToken>())
            .Returns((OrganizationMember?)null);

        var command = new AcceptInvitationCommand(userId, "Invited@Example.com", "plain-token");

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(org.Id, result.Value.OrganizationId);
        Assert.Equal(org.Name, result.Value.OrganizationName);
        Assert.Equal(MembershipRole.Admin, result.Value.Role);
        Assert.Equal(InvitationStatus.Accepted, invitation.Status);

        await _organizationRepository.Received(1).AddMemberAsync(
            Arg.Is<OrganizationMember>(m =>
                m.OrganizationId == org.Id &&
                m.UserId == userId &&
                m.Role == MembershipRole.Admin),
            Arg.Any<CancellationToken>());
        await _organizationRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnForbidden_WhenEmailDoesNotMatchInvitation()
    {
        var org = Organization.Create("Nile Properties", FixedNow.AddDays(-10));
        var invitation = Invitation.Create(
            org.Id,
            "intended@example.com",
            MembershipRole.Staff,
            "hashed-token",
            FixedNow.AddDays(5),
            FixedNow.AddDays(-1));

        _jwtTokenGenerator.HashToken("plain-token").Returns("hashed-token");
        _organizationRepository.GetInvitationByTokenHashAsync("hashed-token", Arg.Any<CancellationToken>())
            .Returns(invitation);

        var command = new AcceptInvitationCommand(Guid.NewGuid(), "other@example.com", "plain-token");
        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Invitation.EmailMismatch", result.Errors.First().Code);
        Assert.Equal(ErrorType.Forbidden, result.Errors.First().Type);
    }

    [Fact]
    public async Task Handle_ShouldReactivateExistingInactiveMember()
    {
        var org = Organization.Create("Nile Properties", FixedNow.AddDays(-20));
        var userId = Guid.NewGuid();
        var member = OrganizationMember.Create(org.Id, userId, MembershipRole.Staff, FixedNow.AddDays(-15));
        member.Deactivate(FixedNow.AddDays(-5));

        var invitation = Invitation.Create(
            org.Id,
            "user@example.com",
            MembershipRole.PropertyManager,
            "hashed-token",
            FixedNow.AddDays(5),
            FixedNow.AddDays(-1));

        _jwtTokenGenerator.HashToken("plain-token").Returns("hashed-token");
        _organizationRepository.GetInvitationByTokenHashAsync("hashed-token", Arg.Any<CancellationToken>())
            .Returns(invitation);
        _organizationRepository.GetByIdAsync(org.Id, Arg.Any<CancellationToken>()).Returns(org);
        _organizationRepository.GetMemberAsync(org.Id, userId, Arg.Any<CancellationToken>()).Returns(member);

        var command = new AcceptInvitationCommand(userId, "user@example.com", "plain-token");
        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(MembershipStatus.Active, member.Status);
        Assert.Equal(MembershipRole.PropertyManager, member.Role);
        Assert.Equal(InvitationStatus.Accepted, invitation.Status);
        await _organizationRepository.DidNotReceive().AddMemberAsync(Arg.Any<OrganizationMember>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnValidationError_WhenInvitationExpired()
    {
        var orgId = Guid.NewGuid();
        var invitation = Invitation.Create(
            orgId,
            "user@example.com",
            MembershipRole.Staff,
            "hashed-token",
            FixedNow.AddMinutes(-1),
            FixedNow.AddDays(-7));

        _jwtTokenGenerator.HashToken("plain-token").Returns("hashed-token");
        _organizationRepository.GetInvitationByTokenHashAsync("hashed-token", Arg.Any<CancellationToken>())
            .Returns(invitation);

        var command = new AcceptInvitationCommand(Guid.NewGuid(), "user@example.com", "plain-token");
        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Invitation.Expired", result.Errors.First().Code);
    }
}
