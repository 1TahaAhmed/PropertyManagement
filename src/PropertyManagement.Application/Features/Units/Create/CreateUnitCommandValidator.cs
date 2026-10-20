using FluentValidation;

namespace PropertyManagement.Application.Features.Units.Create;

public sealed class CreateUnitCommandValidator : AbstractValidator<CreateUnitCommand>
{
    public CreateUnitCommandValidator()
    {
        RuleFor(c => c.TenantId).NotEmpty();
        RuleFor(c => c.PropertyId).NotEmpty();
        RuleFor(c => c.UnitNumber).NotEmpty().MaximumLength(50);
        RuleFor(c => c.Type).IsInEnum();
        RuleFor(c => c.MarketRent).GreaterThanOrEqualTo(0);
        RuleFor(c => c.Bedrooms).GreaterThanOrEqualTo(0);
        RuleFor(c => c.Bathrooms).GreaterThanOrEqualTo(0);
        RuleFor(c => c.SquareMeters).GreaterThanOrEqualTo(0);
        RuleFor(c => c.Description).MaximumLength(1000);
    }
}