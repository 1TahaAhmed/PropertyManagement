using PropertyManagement.Application.Features.Authentication.Registeration;
using PropertyManagement.Application.Validators.Authentication;

namespace PropertyManagement.Application.Tests;

public sealed class RegisterCommandValidatorTests
{
    private readonly RegisterCommandValidator _validator = new();

    [Fact]
    public async Task Validate_ShouldFail_WhenEmailIsInvalid()
    {
        // Arrange
        var command = new RegisterCommand(
            FirstName: "Taha",
            LastName: "Ahmed",
            Address: "",
            Email: "invalid-email",
            Password: "01034287863#Ta",
            ConfirmPassword: "01034287863#Ta");

        // Act
        var result = await _validator.ValidateAsync(command);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            error => error.PropertyName == nameof(command.Email));
    }

    [Fact]
    public async Task Validate_ShouldFail_WhenPasswordsDoNotMatch()
    {
        // Arrange
        var command = new RegisterCommand(
            FirstName: "Taha",
            LastName: "Ahmed",
            Address: "",
            Email: "taha@example.com",
            Password: "01034287863#Ta",
            ConfirmPassword: "DifferentPassword#1");

        // Act
        var result = await _validator.ValidateAsync(command);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            error =>
                error.PropertyName ==
                nameof(command.ConfirmPassword));
    }

    [Fact]
    public async Task Validate_ShouldPass_WhenCommandIsValid()
    {
        // Arrange
        var command = new RegisterCommand(
            FirstName: "Taha",
            LastName: "Ahmed",
            Address: "",
            Email: "taha@example.com",
            Password: "P@ssw0rd123!",
            ConfirmPassword: "01034287863#Ta");

        // Act
        var result = await _validator.ValidateAsync(command);

        // Assert
        Assert.True(result.IsValid);
    }
}
