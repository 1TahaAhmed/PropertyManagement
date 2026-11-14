using FluentValidation;

namespace PropertyManagement.Application.Features.Leases.Renew;

public sealed class RenewLeaseCommandValidator : AbstractValidator<RenewLeaseCommand>
{
    public RenewLeaseCommandValidator()
    {
        RuleFor(x => x.OrganizationId)
            .NotEmpty().WithMessage("Organization ID is required.");

        RuleFor(x => x.LeaseId)
            .NotEmpty().WithMessage("Lease ID is required.");

        RuleFor(x => x.NewEndDate)
            .NotEmpty().WithMessage("New end date is required.");

        RuleFor(x => x.NewRentAmount)
            .GreaterThan(0).WithMessage("Rent amount must be greater than zero.")
            .When(x => x.NewRentAmount.HasValue);

        RuleFor(x => x.NewSecurityDeposit)
            .GreaterThanOrEqualTo(0).WithMessage("Security deposit cannot be negative.")
            .When(x => x.NewSecurityDeposit.HasValue);

        RuleFor(x => x.NewFrequency)
            .IsInEnum().WithMessage("Invalid rent frequency.")
            .When(x => x.NewFrequency.HasValue);

        RuleFor(x => x.NewDueDayOfMonth)
            .InclusiveBetween(1, 28).WithMessage("Due day of month must be between 1 and 28.")
            .When(x => x.NewDueDayOfMonth.HasValue);

        RuleFor(x => x.NewGracePeriodDays)
            .GreaterThanOrEqualTo(0).WithMessage("Grace period days cannot be negative.")
            .When(x => x.NewGracePeriodDays.HasValue);

        RuleFor(x => x.NewLateFeePercentage)
            .InclusiveBetween(0m, 100m).WithMessage("Late fee percentage must be between 0 and 100.")
            .When(x => x.NewLateFeePercentage.HasValue);
    }
}
