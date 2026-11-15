using FluentValidation;

namespace PropertyManagement.Application.Features.Billing.Generate;

public sealed class GenerateLeaseInvoiceCommandValidator : AbstractValidator<GenerateLeaseInvoiceCommand>
{
    public GenerateLeaseInvoiceCommandValidator()
    {
        RuleFor(x => x.OrganizationId)
            .NotEmpty().WithMessage("Organization ID is required.");

        RuleFor(x => x.LeaseId)
            .NotEmpty().WithMessage("Lease ID is required.");

        RuleFor(x => x.PeriodStart)
            .NotEmpty().WithMessage("PeriodStart is required.");

        RuleFor(x => x.PeriodEnd)
            .NotEmpty().WithMessage("PeriodEnd is required.")
            .Must((cmd, periodEnd) => periodEnd >= cmd.PeriodStart)
            .WithMessage("PeriodEnd cannot be earlier than PeriodStart.");

        RuleFor(x => x)
            .Must(cmd => cmd.IncludeRent || cmd.IncludePendingCharges || cmd.ChargeIds is { Count: > 0 } || cmd.AdditionalCharges is { Count: > 0 })
            .WithMessage("At least rent, pending charges, or additional charges must be included when generating an invoice.");

        RuleFor(x => x.IdempotencyKey)
            .MaximumLength(150).WithMessage("IdempotencyKey cannot exceed 150 characters.")
            .When(x => !string.IsNullOrWhiteSpace(x.IdempotencyKey));

        RuleFor(x => x.Notes)
            .MaximumLength(2000).WithMessage("Notes cannot exceed 2000 characters.")
            .When(x => !string.IsNullOrWhiteSpace(x.Notes));

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

        RuleFor(x => x.IssueDate)
            .Must(d => d!.Value.Year is >= 2000 and <= 9999)
            .WithMessage("IssueDate year must be between 2000 and 9999.")
            .When(x => x.IssueDate.HasValue);

        RuleFor(x => x.ChargeIds)
            .Must(ids => ids!.All(id => id != Guid.Empty))
            .WithMessage("ChargeIds cannot contain empty GUIDs.")
            .When(x => x.ChargeIds is not null);
    }
}
