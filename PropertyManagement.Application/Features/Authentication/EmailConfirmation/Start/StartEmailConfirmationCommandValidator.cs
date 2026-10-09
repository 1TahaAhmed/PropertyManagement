using FluentValidation;

namespace PropertyManagement.Application.Features.Authentication.EmailConfirmation.Start;

public sealed class StartEmailConfirmationCommandValidator
    : AbstractValidator<StartEmailConfirmationCommand>
{
    public StartEmailConfirmationCommandValidator()
    {
        RuleFor(command => command.UserId)
            .NotEmpty()
            .WithErrorCode("UserIdRequired")
            .WithMessage("User ID is required.");

        RuleFor(command => command.Token)
            .NotEmpty()
            .WithErrorCode("TokenRequired")
            .WithMessage("Confirmation token is required.");
    }
}
