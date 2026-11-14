using FluentValidation;

namespace PropertyManagement.Application.Features.Leases.Approve;

public sealed class ApproveLeaseCommandValidator : AbstractValidator<ApproveLeaseCommand>
{
    public ApproveLeaseCommandValidator()
    {
        RuleFor(x => x.OrganizationId)
            .NotEmpty().WithMessage("Organization ID is required.");

        RuleFor(x => x.LeaseId)
            .NotEmpty().WithMessage("Lease ID is required.");
    }
}
