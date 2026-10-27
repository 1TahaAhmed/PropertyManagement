using FluentValidation;

namespace PropertyManagement.Application.Features.Properties.Archive;
public sealed class ArchivePropertyCommandValidator : AbstractValidator<ArchivePropertyCommand>
{
    public ArchivePropertyCommandValidator()
    {
        RuleFor(c => c.PropertyId).NotEmpty();
        RuleFor(c => c.TenantId).NotEmpty();
    }
}