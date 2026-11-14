using FluentValidation;

namespace PropertyManagement.Application.Features.Leases.RemoveResident;

public sealed class RemoveResidentFromLeaseCommandValidator : AbstractValidator<RemoveResidentFromLeaseCommand>
{
    public RemoveResidentFromLeaseCommandValidator()
    {
        RuleFor(x => x.OrganizationId).NotEmpty().WithMessage("Organization ID is required.");
        RuleFor(x => x.LeaseId).NotEmpty().WithMessage("Lease ID is required.");
        RuleFor(x => x.ResidentId).NotEmpty().WithMessage("Resident ID is required.");
    }
}
