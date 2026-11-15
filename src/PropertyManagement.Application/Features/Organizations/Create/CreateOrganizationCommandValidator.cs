using FluentValidation;
using PropertyManagement.Domain.Common;

namespace PropertyManagement.Application.Features.Organizations.Create;

public sealed class CreateOrganizationCommandValidator : AbstractValidator<CreateOrganizationCommand>
{
    public CreateOrganizationCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("User ID is required.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Organization name is required.")
            .MaximumLength(100).WithMessage("Organization name must not exceed 100 characters.");

        RuleFor(x => x.CustomSlug)
            .MaximumLength(100).WithMessage("Identifier must not exceed 100 characters.")
            .Matches("^[a-z0-9-]+$").WithMessage("Identifier can only contain lowercase letters, numbers, and hyphens.")
            .When(x => !string.IsNullOrWhiteSpace(x.CustomSlug));

        RuleFor(x => x.DefaultCurrency)
            .Must(CurrencyCode.IsValid)
            .WithMessage("Default currency must be a valid 3-letter ISO currency code (e.g., USD, EGP).");
    }
}
