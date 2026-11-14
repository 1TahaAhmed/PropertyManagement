using FluentValidation;

namespace PropertyManagement.Application.Features.Leases.ChangeResidentRole;

public sealed class ChangeLeaseResidentRoleCommandValidator : AbstractValidator<ChangeLeaseResidentRoleCommand>
{
    public ChangeLeaseResidentRoleCommandValidator()
    {
        RuleFor(x => x.OrganizationId).NotEmpty().WithMessage("Organization ID is required.");
        RuleFor(x => x.LeaseId).NotEmpty().WithMessage("Lease ID is required.");
        RuleFor(x => x.ResidentId).NotEmpty().WithMessage("Resident ID is required.");
        RuleFor(x => x.NewRole).IsInEnum().WithMessage("Invalid lease resident role.");
    }
}
