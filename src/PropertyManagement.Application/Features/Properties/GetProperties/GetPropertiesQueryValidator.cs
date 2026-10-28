using FluentValidation;

namespace PropertyManagement.Application.Features.Properties.GetProperties;

public sealed class GetPropertiesQueryValidator : AbstractValidator<GetPropertiesQuery>
{
    public GetPropertiesQueryValidator()
    {
        RuleFor(x => x.OrganizationId).NotEmpty();
        RuleFor(x => x.PageNumber).GreaterThan(0).WithMessage("Page number must be at least 1.");
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100).WithMessage("Page size must be between 1 and 100.");
    }
}
