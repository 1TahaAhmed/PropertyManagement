using FluentValidation;

namespace PropertyManagement.Application.Features.Residents.Status;

public sealed class BlacklistResidentCommandValidator : AbstractValidator<BlacklistResidentCommand>
{
    public BlacklistResidentCommandValidator()
    {
        RuleFor(x => x.ResidentId).NotEmpty().WithMessage("Resident ID is required.");
        RuleFor(x => x.OrganizationId).NotEmpty().WithMessage("Organization ID is required.");
        RuleFor(x => x.Reason)
            .MaximumLength(500).WithMessage("Reason must not exceed 500 characters.")
            .When(x => !string.IsNullOrWhiteSpace(x.Reason));
    }
}
