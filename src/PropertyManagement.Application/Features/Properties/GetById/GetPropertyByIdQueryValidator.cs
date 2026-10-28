using FluentValidation;

namespace PropertyManagement.Application.Features.Properties.GetById;

public sealed class GetPropertyByIdQueryValidator : AbstractValidator<GetPropertyByIdQuery>
{
    public GetPropertyByIdQueryValidator()
    {
        RuleFor(x => x.TenantId)
            .NotEmpty()
            .WithMessage("Tenant ID cannot be empty.");

        RuleFor(x => x.PropertyId)
            .NotEmpty()
            .WithMessage("Property ID cannot be empty.");
    }
}