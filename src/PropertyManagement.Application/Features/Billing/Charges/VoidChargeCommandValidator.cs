using FluentValidation;

namespace PropertyManagement.Application.Features.Billing.Charges;

public sealed class VoidChargeCommandValidator : AbstractValidator<VoidChargeCommand>
{
    public VoidChargeCommandValidator()
    {
        RuleFor(x => x.OrganizationId).NotEmpty();
        RuleFor(x => x.ChargeId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}
