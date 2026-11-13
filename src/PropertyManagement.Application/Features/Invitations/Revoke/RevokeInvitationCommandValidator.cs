using FluentValidation;

namespace PropertyManagement.Application.Features.Invitations.Revoke;

public sealed class RevokeInvitationCommandValidator : AbstractValidator<RevokeInvitationCommand>
{
    public RevokeInvitationCommandValidator()
    {
        RuleFor(x => x.OrganizationId)
            .NotEmpty()
            .WithMessage("Organization ID is required.");

        RuleFor(x => x.InvitationId)
            .NotEmpty()
            .WithMessage("Invitation ID is required.");
    }
}
