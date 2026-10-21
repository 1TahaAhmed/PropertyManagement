using FluentValidation;

namespace PropertyManagement.Application.Features.Units.GetUnitsByProperty;

public sealed class GetUnitsByPropertyQueryValidator : AbstractValidator<GetUnitsByPropertyQuery>
{
    public GetUnitsByPropertyQueryValidator()
    {
        RuleFor(x => x.PropertyId).NotEmpty();
        RuleFor(x => x.PageNumber).GreaterThan(0).WithMessage("Page number must be at least 1.");
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100).WithMessage("Page size must be between 1 and 100.");
    }
}