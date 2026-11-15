using FluentValidation;

namespace PropertyManagement.Application.Features.Billing.Invoices;

public sealed class CreateManualInvoiceCommandValidator : AbstractValidator<CreateManualInvoiceCommand>
{
    public CreateManualInvoiceCommandValidator()
    {
        RuleFor(x => x.OrganizationId).NotEmpty();
        RuleFor(x => x.LeaseId).NotEmpty();
        RuleFor(x => x.PeriodStart).NotEmpty();
        RuleFor(x => x.PeriodEnd)
            .NotEmpty()
            .Must((cmd, periodEnd) => periodEnd >= cmd.PeriodStart)
            .WithMessage("PeriodEnd cannot be earlier than PeriodStart.");
        RuleFor(x => x.DueDate).NotEmpty();
        RuleFor(x => x)
            .Must(cmd => cmd.Lines is { Count: > 0 } || cmd.ChargeIds is { Count: > 0 })
            .WithMessage("A manual invoice must include at least one line or pending charge.");

        RuleFor(x => x.IdempotencyKey)
            .MaximumLength(150)
            .When(x => !string.IsNullOrWhiteSpace(x.IdempotencyKey));

        RuleFor(x => x.Notes)
            .MaximumLength(2000)
            .When(x => !string.IsNullOrWhiteSpace(x.Notes));

        RuleForEach(x => x.Lines!)
            .ChildRules(line =>
            {
                line.RuleFor(l => l.Type).IsInEnum();
                line.RuleFor(l => l.Description).NotEmpty().MaximumLength(500);
                line.RuleFor(l => l.Quantity).GreaterThan(0);
                line.RuleFor(l => l.UnitPrice).GreaterThan(0);
                line.RuleFor(l => l)
                    .Must(l => l.PeriodStart.HasValue == l.PeriodEnd.HasValue)
                    .WithMessage("Line PeriodStart and PeriodEnd must either both be provided or both be omitted.");
                line.RuleFor(l => l)
                    .Must(l => !l.PeriodStart.HasValue || l.PeriodStart.Value <= l.PeriodEnd!.Value)
                    .WithMessage("Line PeriodStart cannot be later than PeriodEnd.");
            })
            .When(x => x.Lines is not null);

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
