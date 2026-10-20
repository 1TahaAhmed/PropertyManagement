using FluentValidation;

namespace PropertyManagement.Application.Features.Units.ChangeStatus;

public sealed class ChangeUnitStatusCommandValidator : AbstractValidator<ChangeUnitStatusCommand>
{
    public ChangeUnitStatusCommandValidator()
    {
        RuleFor(c => c.UnitId).NotEmpty();
        RuleFor(c => c.NewStatus).IsInEnum();
    }
}