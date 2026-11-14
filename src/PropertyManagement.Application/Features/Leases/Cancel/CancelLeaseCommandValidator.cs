using FluentValidation;

namespace PropertyManagement.Application.Features.Leases.Cancel;

public sealed class CancelLeaseCommandValidator : AbstractValidator<CancelLeaseCommand>
{
    public CancelLeaseCommandValidator()
    {
        RuleFor(x => x.OrganizationId)
            .NotEmpty().WithMessage("Organization ID is required.");

        RuleFor(x => x.LeaseId)
            .NotEmpty().WithMessage("Lease ID is required.");
    }
}
