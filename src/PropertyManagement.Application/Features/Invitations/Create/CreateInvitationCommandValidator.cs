using FluentValidation;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Application.Features.Invitations.Create;

public sealed class CreateInvitationCommandValidator : AbstractValidator<CreateInvitationCommand>
{
    public CreateInvitationCommandValidator()
    {
        RuleFor(x => x.OrganizationId)
            .NotEmpty()
            .WithMessage("Organization ID is required.");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("A valid email address is required.")
            .MaximumLength(256).WithMessage("Email must not exceed 256 characters.");

        RuleFor(x => x.Role)
            .IsInEnum().WithMessage("A valid membership role is required.")
            .NotEqual(MembershipRole.Owner).WithMessage("Cannot invite a member as Owner.");
    }
}
