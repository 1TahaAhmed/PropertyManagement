using FluentValidation;

namespace PropertyManagement.Application.Features.Properties.Update;

public sealed class UpdatePropertyCommandValidator : AbstractValidator<UpdatePropertyCommand>
{
    public UpdatePropertyCommandValidator()
    {
        RuleFor(c => c.PropertyId).NotEmpty();
        RuleFor(c => c.Name).NotEmpty().MaximumLength(150);
        RuleFor(c => c.Address).NotEmpty().MaximumLength(300);
        RuleFor(c => c.Type).IsInEnum();
        RuleFor(c => c.Description).MaximumLength(1000);
    }
}