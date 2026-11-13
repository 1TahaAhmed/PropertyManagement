using NSubstitute;
using PropertyManagement.Application.Abstractions;
using PropertyManagement.Application.Abstractions.Authentication;
using PropertyManagement.Application.Abstractions.Identity;
using PropertyManagement.Application.Abstractions.Organizations;
using PropertyManagement.Application.Common.Enums;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Application.Features.Authentication.SwitchOrganization;
using PropertyManagement.Domain.Entities.Authentication;
using PropertyManagement.Domain.Entities.Organizations;
using PropertyManagement.Domain.Enums;
using Xunit;

namespace PropertyManagement.Application.Tests.Organizations;

public class SwitchOrganizationCommandHandlerTests
{
    private static readonly DateTime FixedNow = new(2026, 11, 13, 12, 0, 0, DateTimeKind.Utc);
    private readonly IIdentityService _identityService = Substitute.For<IIdentityService>();
    private readonly IOrganizationRepository _organizationRepository = Substitute.For<IOrganizationRepository>();
    private readonly IJwtTokenGenerator _jwtTokenGenerator = Substitute.For<IJwtTokenGenerator>();
    private readonly IRefreshTokenRepository _refreshTokenRepository = Substitute.For<IRefreshTokenRepository>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly SwitchOrganizationCommandHandler _handler;

    public SwitchOrganizationCommandHandlerTests()
    {
        _clock.UtcNow.Returns(FixedNow);
        _handler = new SwitchOrganizationCommandHandler(
            _identityService,
            _organizationRepository,
            _jwtTokenGenerator,
            _refreshTokenRepository,
            _clock);
    }

    [Fact]
    public async Task Handle_ShouldIssueScopedTokensAndRevokeOldRefreshToken_WhenUserIsMember()
    {
        var userId = Guid.NewGuid();
        var orgA = Organization.Create("Company A", FixedNow.AddDays(-20));
        var orgB = Organization.Create("Company B", FixedNow.AddDays(-10));
        var user = new AuthUserResult(userId, "user@example.com", "Taha", "Ahmed");
        var existingRefresh = RefreshToken.Create(userId, "old-hash", FixedNow.AddDays(5), FixedNow.AddDays(-1), orgA.Id);

        _identityService.GetAuthUserByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(Result<AuthUserResult>.Success(user));
        _organizationRepository.GetUserOrganizationsAsync(userId, Arg.Any<CancellationToken>())
            .Returns([(orgA, [MembershipRole.Owner]), (orgB, [MembershipRole.PropertyManager])]);
        _jwtTokenGenerator.GenerateAccessToken(userId, user.Email, user.FirstName, user.LastName, orgB.Id, "PropertyManager")
            .Returns(("new-access-token", FixedNow.AddMinutes(15)));
        _jwtTokenGenerator.GenerateRefreshToken()
            .Returns(("new-refresh-plain", "new-refresh-hash", FixedNow.AddDays(7)));
        _jwtTokenGenerator.HashToken("old-refresh-plain").Returns("old-hash");
        _refreshTokenRepository.GetByTokenHashAsync("old-hash", Arg.Any<CancellationToken>())
            .Returns(existingRefresh);

        var command = new SwitchOrganizationCommand(userId, orgB.Id, "old-refresh-plain");
        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("new-access-token", result.Value.AccessToken);
        Assert.Equal("new-refresh-plain", result.Value.RefreshToken);
        Assert.Equal(orgB.Id, result.Value.OrganizationId);
        Assert.Equal("PropertyManager", result.Value.Role);
        Assert.True(existingRefresh.IsRevoked);
        Assert.Equal("new-refresh-hash", existingRefresh.ReplacedByTokenHash);

        await _refreshTokenRepository.Received(1).AddAsync(
            Arg.Is<RefreshToken>(t => t.OrganizationId == orgB.Id && t.TokenHash == "new-refresh-hash"),
            Arg.Any<CancellationToken>());
        await _refreshTokenRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnForbidden_WhenUserIsNotMemberOfTargetOrganization()
    {
        var userId = Guid.NewGuid();
        var orgA = Organization.Create("Company A", FixedNow);
        var targetOrgId = Guid.NewGuid();
        var user = new AuthUserResult(userId, "user@example.com", "Taha", "Ahmed");

        _identityService.GetAuthUserByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(Result<AuthUserResult>.Success(user));
        _organizationRepository.GetUserOrganizationsAsync(userId, Arg.Any<CancellationToken>())
            .Returns([(orgA, [MembershipRole.Owner])]);

        var result = await _handler.Handle(new SwitchOrganizationCommand(userId, targetOrgId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Authentication.NotOrganizationMember", result.Errors.First().Code);
        Assert.Equal(ErrorType.Forbidden, result.Errors.First().Type);
    }
}
