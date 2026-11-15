using FluentValidation;

namespace PropertyManagement.Application.Features.Billing.Charges;

public sealed class GetLeasePendingChargesQueryValidator : AbstractValidator<GetLeasePendingChargesQuery>
{
    public GetLeasePendingChargesQueryValidator()
    {
        RuleFor(x => x.OrganizationId).NotEmpty();
        RuleFor(x => x.LeaseId).NotEmpty();
    }
}
