using FluentValidation;

namespace PropertyManagement.Application.Features.Leases.CreateDraft;

public sealed class CreateDraftLeaseCommandValidator : AbstractValidator<CreateDraftLeaseCommand>
{
    public CreateDraftLeaseCommandValidator()
    {
        RuleFor(x => x.OrganizationId)
            .NotEmpty().WithMessage("Organization ID is required.");

        RuleFor(x => x.PropertyId)
            .NotEmpty().WithMessage("Property ID is required.");

        RuleFor(x => x.UnitId)
            .NotEmpty().WithMessage("Unit ID is required.");

        RuleFor(x => x.StartDate)
            .NotEmpty().WithMessage("StartDate is required.");

        RuleFor(x => x.EndDate)
            .NotEmpty().WithMessage("EndDate is required.")
            .Must((cmd, endDate) => endDate > cmd.StartDate)
            .WithMessage("EndDate must be strictly later than StartDate.");

        RuleFor(x => x.RentAmount)
            .GreaterThan(0).WithMessage("Rent amount must be greater than zero.");

        RuleFor(x => x.SecurityDeposit)
            .GreaterThanOrEqualTo(0).WithMessage("Security deposit cannot be negative.");

        RuleFor(x => x.DueDayOfMonth)
            .InclusiveBetween(1, 28).WithMessage("Due day of month must be between 1 and 28.");

        RuleFor(x => x.GracePeriodDays)
            .GreaterThanOrEqualTo(0).WithMessage("Grace period days cannot be negative.");

        RuleFor(x => x.LateFeePercentage)
            .InclusiveBetween(0m, 100m).WithMessage("Late fee percentage must be between 0 and 100.");
    }
}
