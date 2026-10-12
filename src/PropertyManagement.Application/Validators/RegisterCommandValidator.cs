using FluentValidation;
using PropertyManagement.Application.Features.Authentication.Registeration;

namespace PropertyManagement.Application.Validators.Authentication;

public sealed class RegisterCommandValidator
    : AbstractValidator<RegisterCommand>
{
    public RegisterCommandValidator()
    {
        RuleFor(command => command.FirstName)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(command => command.LastName)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(command => command.Address)
            .MaximumLength(250);

        RuleFor(command => command.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(256);

        RuleFor(command => command.Password)
            .NotEmpty();

        RuleFor(command => command.ConfirmPassword)
            .NotEmpty()
            .Equal(command => command.Password)
            .WithMessage("Passwords do not match.");
    }
}
