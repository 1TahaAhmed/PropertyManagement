using FluentValidation;

namespace PropertyManagement.Application.Features.Billing.Invoices;

public sealed class IssueInvoiceCommandValidator : AbstractValidator<IssueInvoiceCommand>
{
    public IssueInvoiceCommandValidator()
    {
        RuleFor(x => x.OrganizationId).NotEmpty();
        RuleFor(x => x.InvoiceId).NotEmpty();
        RuleFor(x => x.IssueDate)
            .Must(d => d!.Value.Year is >= 2000 and <= 9999)
            .WithMessage("IssueDate year must be between 2000 and 9999.")
            .When(x => x.IssueDate.HasValue);
    }
}
