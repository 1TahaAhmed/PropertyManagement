using FluentValidation;

namespace PropertyManagement.Application.Features.Billing.Invoices;

public sealed class MarkOverdueInvoicesCommandValidator : AbstractValidator<MarkOverdueInvoicesCommand>
{
    public MarkOverdueInvoicesCommandValidator()
    {
        RuleFor(x => x.OrganizationId).NotEmpty();
    }
}
