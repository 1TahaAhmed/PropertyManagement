using FluentValidation;

namespace PropertyManagement.Application.Features.Billing.Invoices;

public sealed class IssueInvoiceCommandValidator : AbstractValidator<IssueInvoiceCommand>
{
    public IssueInvoiceCommandValidator()
    {
        RuleFor(x => x.OrganizationId).NotEmpty();
        RuleFor(x => x.InvoiceId).NotEmpty();
    }
}
