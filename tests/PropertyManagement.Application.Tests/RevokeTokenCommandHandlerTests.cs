using NSubstitute;
using PropertyManagement.Application.Abstractions;
using PropertyManagement.Application.Abstractions.Authentication;
using PropertyManagement.Application.Features.Authentication.RevokeToken;
using PropertyManagement.Domain.Entities.Authentication;

namespace PropertyManagement.Application.Tests;

public sealed class RevokeTokenCommandHandlerTests
{
    private readonly IRefreshTokenRepository _refreshTokenRepository = Substitute.For<IRefreshTokenRepository>();
    private readonly IJwtTokenGenerator _jwtTokenGenerator = Substitute.For<IJwtTokenGenerator>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly RevokeTokenCommandHandler _handler;

    private static readonly DateTime FixedNow = new(2026, 10, 29, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid TestUserId = Guid.NewGuid();

    public RevokeTokenCommandHandlerTests()
    {
        _clock.UtcNow.Returns(FixedNow);
        _jwtTokenGenerator.HashToken("sample_token").Returns("sample_token_hash");

        _handler = new RevokeTokenCommandHandler(
            _refreshTokenRepository,
            _jwtTokenGenerator,
            _clock);
    }

    [Fact]
    public async Task Handle_ShouldRevokeToken_WhenTokenIsActive()
    {
        var command = new RevokeTokenCommand("sample_token");
        var token = RefreshToken.Create(
            TestUserId,
            "sample_token_hash",
            FixedNow.AddDays(7),
            FixedNow);

        _refreshTokenRepository.GetByTokenHashAsync("sample_token_hash", Arg.Any<CancellationToken>())
            .Returns(token);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(token.IsRevoked);
        Assert.Equal(FixedNow, token.RevokedAtUtc);
        await _refreshTokenRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldSucceedIdempotently_WhenTokenNotFound()
    {
        var command = new RevokeTokenCommand("sample_token");

        _refreshTokenRepository.GetByTokenHashAsync("sample_token_hash", Arg.Any<CancellationToken>())
            .Returns((RefreshToken?)null);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        await _refreshTokenRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
