using FluentValidation;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Application.Features.Billing.Invoices;

public sealed class ApplyInvoiceAdjustmentCommandValidator : AbstractValidator<ApplyInvoiceAdjustmentCommand>
{
    public ApplyInvoiceAdjustmentCommandValidator()
    {
        RuleFor(x => x.OrganizationId).NotEmpty();
        RuleFor(x => x.InvoiceId).NotEmpty();
        RuleFor(x => x.ActorUserId).NotEmpty();
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.ChargeType).IsInEnum();
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.Description).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);

        RuleFor(x => x.ChargeType)
            .Must(ct => ct is ChargeType.CreditAdjustment or ChargeType.Discount)
            .When(x => x.Type == InvoiceAdjustmentType.Credit)
            .WithMessage("Credit adjustments must use CreditAdjustment or Discount charge type.");

        RuleFor(x => x.ChargeType)
            .Must(ct => ct is not (ChargeType.Rent or ChargeType.Discount or ChargeType.CreditAdjustment))
            .When(x => x.Type == InvoiceAdjustmentType.Debit)
            .WithMessage("Debit adjustments cannot use Rent, Discount, or CreditAdjustment charge types.");
    }
}
