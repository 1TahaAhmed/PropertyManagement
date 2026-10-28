using FluentValidation;

namespace PropertyManagement.Application.Features.Units.GetById;

public sealed class GetUnitByIdQueryValidator : AbstractValidator<GetUnitByIdQuery>
{
    public GetUnitByIdQueryValidator()
    {
        RuleFor(x => x.TenantId)
            .NotEmpty()
            .WithMessage("Tenant ID cannot be empty.");

        RuleFor(x => x.UnitId)
            .NotEmpty()
            .WithMessage("Unit ID cannot be empty.");
    }
}