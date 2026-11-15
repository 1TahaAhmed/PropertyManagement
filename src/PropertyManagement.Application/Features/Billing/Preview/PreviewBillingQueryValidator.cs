using FluentValidation;

namespace PropertyManagement.Application.Features.Billing.Preview;

public sealed class PreviewBillingQueryValidator : AbstractValidator<PreviewBillingQuery>
{
    public PreviewBillingQueryValidator()
    {
        RuleFor(x => x.OrganizationId)
            .NotEmpty().WithMessage("Organization ID is required.");

        RuleFor(x => x.LeaseId)
            .NotEmpty().WithMessage("Lease ID is required.");

        RuleFor(x => x.PeriodStart)
            .NotEmpty().WithMessage("PeriodStart is required.");

        RuleFor(x => x.PeriodEnd)
            .NotEmpty().WithMessage("PeriodEnd is required.")
            .Must((q, periodEnd) => periodEnd >= q.PeriodStart)
            .WithMessage("PeriodEnd cannot be earlier than PeriodStart.");

        RuleFor(x => x)
            .Must(q => q.IncludeRent || q.IncludePendingCharges || q.ChargeIds is { Count: > 0 } || q.AdditionalCharges is { Count: > 0 })
            .WithMessage("At least rent, pending charges, or additional charges must be included in billing preview.");

        RuleForEach(x => x.Discounts!)
            .ChildRules(d =>
            {
                d.RuleFor(x => x.Description).NotEmpty().MaximumLength(500);
                d.RuleFor(x => x.Amount).GreaterThan(0);
            })
            .When(x => x.Discounts is not null);

        RuleForEach(x => x.AdditionalCharges!)
            .ChildRules(c =>
            {
                c.RuleFor(x => x.Type).IsInEnum();
                c.RuleFor(x => x.Description).NotEmpty().MaximumLength(500);
                c.RuleFor(x => x.Quantity).GreaterThan(0);
                c.RuleFor(x => x.UnitPrice).GreaterThan(0);
                c.RuleFor(x => x)
                    .Must(x => x.PeriodStart.HasValue == x.PeriodEnd.HasValue)
                    .WithMessage("Charge PeriodStart and PeriodEnd must either both be provided or both be omitted.");
                c.RuleFor(x => x)
                    .Must(x => !x.PeriodStart.HasValue || x.PeriodStart.Value <= x.PeriodEnd!.Value)
                    .WithMessage("Charge PeriodStart cannot be later than PeriodEnd.");
            })
            .When(x => x.AdditionalCharges is not null);

        RuleFor(x => x.ChargeIds)
            .Must(ids => ids!.All(id => id != Guid.Empty))
            .WithMessage("ChargeIds cannot contain empty GUIDs.")
            .When(x => x.ChargeIds is not null);
    }
}
