using NSubstitute;
using PropertyManagement.Application.Abstractions;
using PropertyManagement.Application.Abstractions.Authentication;
using PropertyManagement.Application.Abstractions.Identity;
using PropertyManagement.Application.Abstractions.Organizations;
using PropertyManagement.Application.Common.Enums;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Application.Features.Authentication.Login;
using PropertyManagement.Domain.Entities.Authentication;
using PropertyManagement.Domain.Entities.Organizations;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Application.Tests;

public sealed class LoginCommandHandlerTests
{
    private readonly IIdentityService _identityService = Substitute.For<IIdentityService>();
    private readonly IOrganizationRepository _organizationRepository = Substitute.For<IOrganizationRepository>();
    private readonly IJwtTokenGenerator _jwtTokenGenerator = Substitute.For<IJwtTokenGenerator>();
    private readonly IRefreshTokenRepository _refreshTokenRepository = Substitute.For<IRefreshTokenRepository>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly LoginCommandHandler _handler;

    private static readonly DateTime FixedNow = new(2026, 10, 29, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid TestUserId = Guid.NewGuid();

    public LoginCommandHandlerTests()
    {
        _clock.UtcNow.Returns(FixedNow);
        _handler = new LoginCommandHandler(
            _identityService,
            _organizationRepository,
            _jwtTokenGenerator,
            _refreshTokenRepository,
            _clock);
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenCredentialsAreInvalid()
    {
        var command = new LoginCommand("test@example.com", "WrongPassword!");

        _identityService.ValidateCredentialsAsync(
            command.Email,
            command.Password,
            Arg.Any<CancellationToken>())
            .Returns(Result<AuthUserResult>.Failure(
                new Error(
                    Code: "Identity.InvalidCredentials",
                    Message: "Invalid email or password.",
                    Type: ErrorType.Validation)));

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Identity.InvalidCredentials", result.Errors.First().Code);
        await _refreshTokenRepository.DidNotReceive().AddAsync(Arg.Any<RefreshToken>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenUserIsLockedOut()
    {
        var command = new LoginCommand("test@example.com", "Password123!");

        _identityService.ValidateCredentialsAsync(
            command.Email,
            command.Password,
            Arg.Any<CancellationToken>())
            .Returns(Result<AuthUserResult>.Failure(
                new Error(
                    Code: "Identity.UserLockedOut",
                    Message: "Account is locked out.",
                    Type: ErrorType.Failure)));

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Identity.UserLockedOut", result.Errors.First().Code);
    }

    [Fact]
    public async Task Handle_ShouldReturnSuccessWithTokens_WhenCredentialsAreValid()
    {
        var command = new LoginCommand("test@example.com", "Password123!");
        var authUser = new AuthUserResult(TestUserId, "test@example.com", "Test", "User");
        var organization = Organization.Create("Test Org", FixedNow);

        _identityService.ValidateCredentialsAsync(
            command.Email,
            command.Password,
            Arg.Any<CancellationToken>())
            .Returns(Result<AuthUserResult>.Success(authUser));

        _organizationRepository.GetUserOrganizationsAsync(
            TestUserId,
            Arg.Any<CancellationToken>())
            .Returns([(organization, [MembershipRole.Owner])]);

        _jwtTokenGenerator.GenerateAccessToken(
            TestUserId,
            "test@example.com",
            "Test",
            "User",
            organization.Id,
            "Owner")
            .Returns("access_token_mock");

        _jwtTokenGenerator.GenerateRefreshToken()
            .Returns(("plain_refresh_token", "hashed_token", FixedNow.AddDays(7)));

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("access_token_mock", result.Value.AccessToken);
        Assert.Equal("plain_refresh_token", result.Value.RefreshToken);
        Assert.Equal(organization.Id, result.Value.OrganizationId);
        Assert.Equal("Owner", result.Value.Role);

        await _refreshTokenRepository.Received(1).AddAsync(
            Arg.Is<RefreshToken>(t => t.UserId == TestUserId && t.TokenHash == "hashed_token"),
            Arg.Any<CancellationToken>());
        await _refreshTokenRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenUserIsNotMemberOfRequestedOrganization()
    {
        var requestedOrgId = Guid.NewGuid();
        var command = new LoginCommand("test@example.com", "Password123!", requestedOrgId);
        var authUser = new AuthUserResult(TestUserId, "test@example.com", "Test", "User");

        _identityService.ValidateCredentialsAsync(
            command.Email,
            command.Password,
            Arg.Any<CancellationToken>())
            .Returns(Result<AuthUserResult>.Success(authUser));

        _organizationRepository.GetUserOrganizationsAsync(
            TestUserId,
            Arg.Any<CancellationToken>())
            .Returns([]); // Not a member of any org

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Authentication.NotOrganizationMember", result.Errors.First().Code);
    }
}
