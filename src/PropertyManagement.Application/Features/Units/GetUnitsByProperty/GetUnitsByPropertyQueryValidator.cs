using FluentValidation;

namespace PropertyManagement.Application.Features.Units.GetUnitsByProperty;

public sealed class GetUnitsByPropertyQueryValidator : AbstractValidator<GetUnitsByPropertyQuery>
{
    public GetUnitsByPropertyQueryValidator()
    {
        RuleFor(x => x.TenantId)
            .NotEmpty()
            .WithMessage("Tenant ID cannot be empty.");

        RuleFor(x => x.PropertyId)
            .NotEmpty()
            .WithMessage("Property ID cannot be empty.");

        RuleFor(x => x.PageNumber).GreaterThan(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);

        RuleFor(x => x.MinRent)
            .GreaterThanOrEqualTo(0).When(x => x.MinRent.HasValue);

        RuleFor(x => x.MaxRent)
            .GreaterThanOrEqualTo(0).When(x => x.MaxRent.HasValue)
            .GreaterThanOrEqualTo(x => x.MinRent!.Value)
            .When(x => x.MinRent.HasValue && x.MaxRent.HasValue)
            .WithMessage("Maximum rent must be greater than or equal to minimum rent.");
    }
}