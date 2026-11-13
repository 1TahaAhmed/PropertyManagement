using FluentValidation;

namespace PropertyManagement.Application.Features.Organizations.Members;

public sealed class ChangeMemberRoleCommandValidator : AbstractValidator<ChangeMemberRoleCommand>
{
    public ChangeMemberRoleCommandValidator()
    {
        RuleFor(x => x.OrganizationId).NotEmpty();
        RuleFor(x => x.MemberId).NotEmpty();
        RuleFor(x => x.NewRole).IsInEnum().WithMessage("A valid membership role is required.");
    }
}
