using FluentValidation;

namespace PropertyManagement.Application.Features.Billing.Charges;

public sealed class CreateChargeCommandValidator : AbstractValidator<CreateChargeCommand>
{
    public CreateChargeCommandValidator()
    {
        RuleFor(x => x.OrganizationId).NotEmpty();
        RuleFor(x => x.LeaseId).NotEmpty();
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.Description).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Quantity).GreaterThan(0);
        RuleFor(x => x.UnitPrice).GreaterThan(0);
        RuleFor(x => x)
            .Must(x => x.PeriodStart.HasValue == x.PeriodEnd.HasValue)
            .WithMessage("PeriodStart and PeriodEnd must either both be provided or both be omitted.");
        RuleFor(x => x)
            .Must(x => !x.PeriodStart.HasValue || x.PeriodStart.Value <= x.PeriodEnd!.Value)
            .WithMessage("PeriodStart cannot be later than PeriodEnd.");
    }
}
