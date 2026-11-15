using FluentValidation;
using PropertyManagement.Domain.Common;

namespace PropertyManagement.Application.Features.Organizations.Update;

public sealed class UpdateOrganizationCommandValidator : AbstractValidator<UpdateOrganizationCommand>
{
    public UpdateOrganizationCommandValidator()
    {
        RuleFor(x => x.OrganizationId)
            .NotEmpty().WithMessage("Organization ID is required.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Organization name is required.")
            .MaximumLength(100).WithMessage("Organization name must not exceed 100 characters.");

        RuleFor(x => x.DefaultCurrency)
            .Must(CurrencyCode.IsValid)
            .WithMessage("Default currency must be a valid 3-letter ISO currency code (e.g., USD, EGP).")
            .When(x => !string.IsNullOrWhiteSpace(x.DefaultCurrency));
    }
}
