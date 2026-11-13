using FluentValidation;

namespace PropertyManagement.Application.Features.Authentication.SwitchOrganization;

public sealed class SwitchOrganizationCommandValidator : AbstractValidator<SwitchOrganizationCommand>
{
    public SwitchOrganizationCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("User ID is required.");

        RuleFor(x => x.TargetOrganizationId)
            .NotEmpty()
            .WithMessage("Target organization ID is required.");
    }
}
