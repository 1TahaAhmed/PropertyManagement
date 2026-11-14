using FluentValidation;

namespace PropertyManagement.Application.Features.Leases.GetById;

public sealed class GetLeaseByIdQueryValidator : AbstractValidator<GetLeaseByIdQuery>
{
    public GetLeaseByIdQueryValidator()
    {
        RuleFor(x => x.OrganizationId)
            .NotEmpty().WithMessage("Organization ID is required.");

        RuleFor(x => x.LeaseId)
            .NotEmpty().WithMessage("Lease ID is required.");
    }
}
