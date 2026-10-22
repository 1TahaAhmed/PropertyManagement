using FluentValidation;

namespace PropertyManagement.Application.Features.Units.Update;

public sealed class UpdateUnitCommandValidator : AbstractValidator<UpdateUnitCommand>
{
    public UpdateUnitCommandValidator()
    {
        RuleFor(c => c.UnitId).NotEmpty();
        RuleFor(c => c.TenantId).NotEmpty();
        RuleFor(c => c.UnitNumber).NotEmpty().MaximumLength(50);
        RuleFor(c => c.Type).IsInEnum();
        RuleFor(c => c.MarketRent).GreaterThanOrEqualTo(0);
        RuleFor(c => c.Bedrooms).GreaterThanOrEqualTo(0);
        RuleFor(c => c.Bathrooms).GreaterThanOrEqualTo(0);
        RuleFor(c => c.SquareMeters).GreaterThanOrEqualTo(0);
        RuleFor(c => c.Description).MaximumLength(1000);
        RuleFor(c => c.RowVersion).NotEmpty().WithMessage("Concurrency token (RowVersion) is required.");
    }
}
