using FluentValidation;

namespace PropertyManagement.Application.Features.Organizations.Register;

public sealed class RegisterOrganizationCommandValidator : AbstractValidator<RegisterOrganizationCommand>
{
    public RegisterOrganizationCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Organization name is required.")
            .MaximumLength(200).WithMessage("Organization name must not exceed 200 characters.");

        RuleFor(x => x.OwnerUserId)
            .NotEmpty().WithMessage("Owner user ID is required.");
    }
}