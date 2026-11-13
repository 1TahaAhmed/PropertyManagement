using NSubstitute;
using PropertyManagement.Application.Abstractions;
using PropertyManagement.Application.Abstractions.Organizations;
using PropertyManagement.Application.Common.Enums;
using PropertyManagement.Application.Features.Organizations.RevokeInvitation;
using PropertyManagement.Domain.Entities.Organizations;
using PropertyManagement.Domain.Enums;
using Xunit;

namespace PropertyManagement.Application.Tests.Organizations;

public class RevokeInvitationCommandHandlerTests
{
    private static readonly DateTime FixedNow = new(2026, 11, 13, 12, 0, 0, DateTimeKind.Utc);
    private readonly IOrganizationRepository _organizationRepository = Substitute.For<IOrganizationRepository>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly RevokeInvitationCommandHandler _handler;

    public RevokeInvitationCommandHandlerTests()
    {
        _clock.UtcNow.Returns(FixedNow);
        _handler = new RevokeInvitationCommandHandler(_organizationRepository, _clock);
    }

    [Fact]
    public async Task Handle_ShouldRevokePendingInvitation()
    {
        var orgId = Guid.NewGuid();
        var invitation = Invitation.Create(
            orgId,
            "user@example.com",
            MembershipRole.Staff,
            "hash",
            FixedNow.AddDays(7),
            FixedNow.AddDays(-1));

        _organizationRepository.GetInvitationByIdAsync(invitation.Id, orgId, Arg.Any<CancellationToken>())
            .Returns(invitation);

        var result = await _handler.Handle(new RevokeInvitationCommand(orgId, invitation.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(InvitationStatus.Revoked, invitation.Status);
        await _organizationRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnNotFound_WhenInvitationDoesNotExist()
    {
        var result = await _handler.Handle(
            new RevokeInvitationCommand(Guid.NewGuid(), Guid.NewGuid()),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.NotFound, result.Errors.First().Type);
    }
}
