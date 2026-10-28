using FluentValidation;

namespace PropertyManagement.Application.Features.Properties.Create;

public sealed class CreatePropertyCommandValidator : AbstractValidator<CreatePropertyCommand>
{
    public CreatePropertyCommandValidator()
    {
        RuleFor(c => c.OrganizationId)
            .NotEmpty()
            .WithMessage("Organization ID is required.");

        RuleFor(c => c.Name)
            .NotEmpty()
            .MaximumLength(150);

        RuleFor(c => c.Address)
            .NotEmpty()
            .MaximumLength(300);

        RuleFor(c => c.Type)
            .IsInEnum();

        RuleFor(c => c.Description)
            .MaximumLength(1000);
    }
}
