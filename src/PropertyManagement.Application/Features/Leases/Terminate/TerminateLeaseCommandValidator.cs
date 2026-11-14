using FluentValidation;

namespace PropertyManagement.Application.Features.Leases.Terminate;

public sealed class TerminateLeaseCommandValidator : AbstractValidator<TerminateLeaseCommand>
{
    public TerminateLeaseCommandValidator()
    {
        RuleFor(x => x.OrganizationId)
            .NotEmpty().WithMessage("Organization ID is required.");

        RuleFor(x => x.LeaseId)
            .NotEmpty().WithMessage("Lease ID is required.");

        RuleFor(x => x.TerminationDate)
            .NotEmpty().WithMessage("Termination date is required.");

        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Termination reason is required.")
            .MaximumLength(500).WithMessage("Termination reason cannot exceed 500 characters.");

        RuleFor(x => x.TerminatedByUserId)
            .NotEmpty().WithMessage("Terminating user ID is required.");

        RuleFor(x => x.Notes)
            .MaximumLength(2000).WithMessage("Termination notes cannot exceed 2000 characters.")
            .When(x => x.Notes is not null);
    }
}
