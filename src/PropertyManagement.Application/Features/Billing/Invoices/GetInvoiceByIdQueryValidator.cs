using FluentValidation;

namespace PropertyManagement.Application.Features.Billing.Invoices;

public sealed class GetInvoiceByIdQueryValidator : AbstractValidator<GetInvoiceByIdQuery>
{
    public GetInvoiceByIdQueryValidator()
    {
        RuleFor(x => x.OrganizationId).NotEmpty();
        RuleFor(x => x.InvoiceId).NotEmpty();
    }
}
