using FluentValidation;

namespace PropertyManagement.Application.Features.Leases.Submit;

public sealed class SubmitLeaseForApprovalCommandValidator : AbstractValidator<SubmitLeaseForApprovalCommand>
{
    public SubmitLeaseForApprovalCommandValidator()
    {
        RuleFor(x => x.OrganizationId)
            .NotEmpty().WithMessage("Organization ID is required.");

        RuleFor(x => x.LeaseId)
            .NotEmpty().WithMessage("Lease ID is required.");
    }
}
