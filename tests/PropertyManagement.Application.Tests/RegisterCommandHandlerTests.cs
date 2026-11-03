using NSubstitute;
using PropertyManagement.Application.Abstractions.Email;
using PropertyManagement.Application.Abstractions.Identity;
using PropertyManagement.Application.Common.Enums;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Application.Features.Authentication.Registeration;

namespace PropertyManagement.Application.Tests;

public sealed class RegisterCommandHandlerTests
{
    private readonly IIdentityService _identityService = Substitute.For<IIdentityService>();
    private readonly IEmailConfirmationCodeIssuer _codeIssuer = Substitute.For<IEmailConfirmationCodeIssuer>();
    private readonly RegisterCommandHandler _handler;

    private static readonly DateTime FixedNow = new(2026, 10, 29, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid TestUserId = Guid.NewGuid();

    public RegisterCommandHandlerTests()
    {
        _handler = new RegisterCommandHandler(_identityService, _codeIssuer);
    }

    [Fact]
    public async Task Handle_ShouldReturnSuccess_WhenRegistrationAndEmailIssuanceSucceed()
    {
        var command = new RegisterCommand("John", "Doe", "123 Main St", "john@example.com", "Password123!", "Password123!");

        _identityService.RegisterAsync(
            command.FirstName,
            command.LastName,
            command.Address,
            command.Email,
            command.Password,
            phoneNumber: null,
            Arg.Any<CancellationToken>())
            .Returns(Result<IdentityUserResult>.Success(new IdentityUserResult(TestUserId, FixedNow)));

        _codeIssuer.IssueAsync(TestUserId, command.Email, command.FirstName, Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(TestUserId, result.Value.UserId);
        Assert.Equal("john@example.com", result.Value.Email);
        Assert.False(result.Value.EmailConfirmed);

        await _identityService.DidNotReceive().DeleteUserAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldDeleteUserAndReturnFailure_WhenCodeIssuerFails()
    {
        var command = new RegisterCommand("John", "Doe", "123 Main St", "john@example.com", "Password123!", "Password123!");

        _identityService.RegisterAsync(
            command.FirstName,
            command.LastName,
            command.Address,
            command.Email,
            command.Password,
            phoneNumber: null,
            Arg.Any<CancellationToken>())
            .Returns(Result<IdentityUserResult>.Success(new IdentityUserResult(TestUserId, FixedNow)));

        _codeIssuer.IssueAsync(TestUserId, command.Email, command.FirstName, Arg.Any<CancellationToken>())
            .Returns(Result.Failure(new Error("Email.Failed", "Failed to send", Type: ErrorType.Failure)));

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Email.Failed", result.Errors.First().Code);

        // Security compensation: user MUST be deleted
        await _identityService.Received(1).DeleteUserAsync(TestUserId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldDeleteUserAndReturnFailure_WhenCodeIssuerThrowsException()
    {
        var command = new RegisterCommand("John", "Doe", "123 Main St", "john@example.com", "Password123!", "Password123!");

        _identityService.RegisterAsync(
            command.FirstName,
            command.LastName,
            command.Address,
            command.Email,
            command.Password,
            phoneNumber: null,
            Arg.Any<CancellationToken>())
            .Returns(Result<IdentityUserResult>.Success(new IdentityUserResult(TestUserId, FixedNow)));

        _codeIssuer.IssueAsync(TestUserId, command.Email, command.FirstName, Arg.Any<CancellationToken>())
            .Returns<Task<Result>>(x => throw new InvalidOperationException("SMTP host unavailable"));

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("EmailConfirmation.SendFailed", result.Errors.First().Code);

        // Security compensation: user MUST be deleted to allow re-registration
        await _identityService.Received(1).DeleteUserAsync(TestUserId, Arg.Any<CancellationToken>());
    }
}
