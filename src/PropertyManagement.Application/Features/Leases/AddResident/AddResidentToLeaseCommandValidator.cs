using FluentValidation;

namespace PropertyManagement.Application.Features.Leases.AddResident;

public sealed class AddResidentToLeaseCommandValidator : AbstractValidator<AddResidentToLeaseCommand>
{
    public AddResidentToLeaseCommandValidator()
    {
        RuleFor(x => x.OrganizationId)
            .NotEmpty().WithMessage("Organization ID is required.");

        RuleFor(x => x.LeaseId)
            .NotEmpty().WithMessage("Lease ID is required.");

        RuleFor(x => x.ResidentId)
            .NotEmpty().WithMessage("Resident ID is required.");

        RuleFor(x => x.Role)
            .IsInEnum().WithMessage("Invalid lease resident role.");
    }
}
