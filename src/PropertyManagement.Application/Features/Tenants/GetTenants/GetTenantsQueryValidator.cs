using FluentValidation;

namespace PropertyManagement.Application.Features.Tenants.GetTenants;

public sealed class GetTenantsQueryValidator : AbstractValidator<GetTenantsQuery>
{
    public GetTenantsQueryValidator()
    {
        RuleFor(x => x.OrganizationId)
            .NotEmpty().WithMessage("Organization ID is required.");

        RuleFor(x => x.Status)
            .IsInEnum().WithMessage("Invalid tenant status.")
            .When(x => x.Status.HasValue);

        RuleFor(x => x.PageNumber)
            .GreaterThan(0).WithMessage("Page number must be at least 1.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100).WithMessage("Page size must be between 1 and 100.");
    }
}
