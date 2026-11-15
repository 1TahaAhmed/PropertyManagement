using FluentValidation;

namespace PropertyManagement.Application.Features.Billing.Invoices;

public sealed class GetInvoicesQueryValidator : AbstractValidator<GetInvoicesQuery>
{
    public GetInvoicesQueryValidator()
    {
        RuleFor(x => x.OrganizationId).NotEmpty();
        RuleFor(x => x.Status)
            .IsInEnum()
            .When(x => x.Status.HasValue);
        RuleFor(x => x)
            .Must(x => !x.PeriodStart.HasValue || !x.PeriodEnd.HasValue || x.PeriodStart.Value <= x.PeriodEnd.Value)
            .WithMessage("PeriodStart cannot be later than PeriodEnd.");
        RuleFor(x => x.PageNumber).GreaterThan(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}
