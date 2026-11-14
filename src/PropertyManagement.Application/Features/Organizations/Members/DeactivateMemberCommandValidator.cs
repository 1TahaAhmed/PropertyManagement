using FluentValidation;

namespace PropertyManagement.Application.Features.Organizations.Members;

public sealed class DeactivateMemberCommandValidator : AbstractValidator<DeactivateMemberCommand>
{
    public DeactivateMemberCommandValidator()
    {
        RuleFor(x => x.OrganizationId).NotEmpty();
        RuleFor(x => x.MemberId).NotEmpty();
        RuleFor(x => x.ActorUserId).NotEmpty();
        RuleFor(x => x.ActorRole).IsInEnum().WithMessage("A valid actor role is required.");
    }
}
