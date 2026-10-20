using PropertyManagement.Application.Features.Authentication.Registeration;
using PropertyManagement.Application.Validators.Authentication;

namespace PropertyManagement.Application.Tests;

public sealed class RegisterCommandValidatorTests
{
    private readonly RegisterCommandValidator _validator = new();
    private const string ValidPassword = "P@ssw0rd123!";

    [Fact]
    public async Task Validate_ShouldFail_WhenEmailIsInvalid()
    {
        var command = new RegisterCommand(
            FirstName: "Taha",
            LastName: "Ahmed",
            Address: "",
            Email: "invalid-email",
            Password: ValidPassword,
            ConfirmPassword: ValidPassword);

        var result = await _validator.ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(command.Email));
    }

    [Fact]
    public async Task Validate_ShouldFail_WhenPasswordsDoNotMatch()
    {
        var command = new RegisterCommand(
            FirstName: "Taha",
            LastName: "Ahmed",
            Address: "",
            Email: "taha@example.com",
            Password: ValidPassword,
            ConfirmPassword: "DifferentPassword#999");

        var result = await _validator.ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(command.ConfirmPassword));
    }

    [Fact]
    public async Task Validate_ShouldPass_WhenCommandIsValid()
    {
        var command = new RegisterCommand(
            FirstName: "Taha",
            LastName: "Ahmed",
            Address: "",
            Email: "taha@example.com",
            Password: ValidPassword,
            ConfirmPassword: ValidPassword);

        var result = await _validator.ValidateAsync(command);

        Assert.True(result.IsValid);
    }
}