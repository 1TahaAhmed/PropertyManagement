using FluentValidation;

namespace PropertyManagement.Application.Features.Tenants.Status;

public sealed class BlacklistTenantCommandValidator : AbstractValidator<BlacklistTenantCommand>
{
    public BlacklistTenantCommandValidator()
    {
        RuleFor(x => x.TenantId)
            .NotEmpty().WithMessage("Tenant ID is required.");

        RuleFor(x => x.OrganizationId)
            .NotEmpty().WithMessage("Organization ID is required.");

        RuleFor(x => x.Reason)
            .MaximumLength(500).WithMessage("Reason must not exceed 500 characters.");
    }
}
