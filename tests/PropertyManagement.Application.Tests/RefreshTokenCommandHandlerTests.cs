using NSubstitute;
using PropertyManagement.Application.Abstractions;
using PropertyManagement.Application.Abstractions.Authentication;
using PropertyManagement.Application.Abstractions.Identity;
using PropertyManagement.Application.Abstractions.Organizations;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Application.Features.Authentication.RefreshToken;
using PropertyManagement.Domain.Entities.Authentication;
using PropertyManagement.Domain.Entities.Organizations;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Application.Tests;

public sealed class RefreshTokenCommandHandlerTests
{
    private readonly IRefreshTokenRepository _refreshTokenRepository = Substitute.For<IRefreshTokenRepository>();
    private readonly IIdentityService _identityService = Substitute.For<IIdentityService>();
    private readonly IOrganizationRepository _organizationRepository = Substitute.For<IOrganizationRepository>();
    private readonly IJwtTokenGenerator _jwtTokenGenerator = Substitute.For<IJwtTokenGenerator>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly RefreshTokenCommandHandler _handler;

    private static readonly DateTime FixedNow = new(2026, 10, 29, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid TestUserId = Guid.NewGuid();

    public RefreshTokenCommandHandlerTests()
    {
        _clock.UtcNow.Returns(FixedNow);
        _jwtTokenGenerator.HashToken("raw_plain_token").Returns("hashed_plain_token");
        _jwtTokenGenerator.HashToken("revoked_token").Returns("hashed_revoked_token");
        _jwtTokenGenerator.HashToken("expired_token").Returns("hashed_expired_token");

        _handler = new RefreshTokenCommandHandler(
            _refreshTokenRepository,
            _identityService,
            _organizationRepository,
            _jwtTokenGenerator,
            _clock);
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenTokenDoesNotExist()
    {
        var command = new RefreshTokenCommand("raw_plain_token");

        _refreshTokenRepository.GetByTokenHashAsync("hashed_plain_token", Arg.Any<CancellationToken>())
            .Returns((RefreshToken?)null);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Authentication.InvalidRefreshToken", result.Errors[0].Code);
    }

    [Fact]
    public async Task Handle_ShouldTriggerReuseDetectionAndRevokeAllUserTokens_WhenTokenIsAlreadyRevoked()
    {
        var command = new RefreshTokenCommand("revoked_token");

        var revokedToken = RefreshToken.Create(
            TestUserId,
            "hashed_revoked_token",
            FixedNow.AddDays(7),
            FixedNow.AddDays(-1));
        revokedToken.Revoke(FixedNow.AddHours(-1), "replaced_by_hash");

        _refreshTokenRepository.GetByTokenHashAsync("hashed_revoked_token", Arg.Any<CancellationToken>())
            .Returns(revokedToken);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Authentication.CompromisedRefreshToken", result.Errors[0].Code);

        // Security assertion: All active tokens for the user MUST be revoked
        await _refreshTokenRepository.Received(1).RevokeAllUserTokensAsync(
            TestUserId,
            FixedNow,
            Arg.Any<CancellationToken>());
        await _refreshTokenRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenTokenIsExpired()
    {
        var command = new RefreshTokenCommand("expired_token");

        var expiredToken = RefreshToken.Create(
            TestUserId,
            "hashed_expired_token",
            FixedNow.AddMinutes(-5), // Expired 5 mins ago
            FixedNow.AddDays(-7));

        _refreshTokenRepository.GetByTokenHashAsync("hashed_expired_token", Arg.Any<CancellationToken>())
            .Returns(expiredToken);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Authentication.ExpiredRefreshToken", result.Errors[0].Code);
    }

    [Fact]
    public async Task Handle_ShouldRotateTokenAndReturnNewTokens_WhenTokenIsValid()
    {
        var command = new RefreshTokenCommand("raw_plain_token");

        var activeToken = RefreshToken.Create(
            TestUserId,
            "hashed_plain_token",
            FixedNow.AddDays(7),
            FixedNow);

        _refreshTokenRepository.GetByTokenHashAsync("hashed_plain_token", Arg.Any<CancellationToken>())
            .Returns(activeToken);

        _jwtTokenGenerator.GenerateRefreshToken()
            .Returns(("new_plain_token", "new_hashed_token", FixedNow.AddDays(7)));

        var authUser = new AuthUserResult(TestUserId, "user@example.com", "Test", "User");
        _identityService.GetAuthUserByIdAsync(TestUserId, Arg.Any<CancellationToken>())
            .Returns(Result<AuthUserResult>.Success(authUser));

        var organization = Organization.Create("Acme Org", FixedNow);
        _organizationRepository.GetUserOrganizationsAsync(TestUserId, Arg.Any<CancellationToken>())
            .Returns([(organization, [MembershipRole.Admin])]);

        _jwtTokenGenerator.GenerateAccessToken(
            TestUserId,
            "user@example.com",
            "Test",
            "User",
            organization.Id,
            "Admin")
            .Returns("new_access_token");

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("new_access_token", result.Value.AccessToken);
        Assert.Equal("new_plain_token", result.Value.RefreshToken);

        // Old token should now be revoked with replacement hash
        Assert.True(activeToken.IsRevoked);
        Assert.Equal("new_hashed_token", activeToken.ReplacedByTokenHash);

        // New token persisted
        await _refreshTokenRepository.Received(1).AddAsync(
            Arg.Is<RefreshToken>(t => t.UserId == TestUserId && t.TokenHash == "new_hashed_token"),
            Arg.Any<CancellationToken>());
        await _refreshTokenRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
