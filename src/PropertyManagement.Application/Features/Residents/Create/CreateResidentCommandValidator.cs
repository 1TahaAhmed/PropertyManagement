using FluentValidation;

namespace PropertyManagement.Application.Features.Residents.Create;

public sealed class CreateResidentCommandValidator : AbstractValidator<CreateResidentCommand>
{
    public CreateResidentCommandValidator()
    {
        RuleFor(x => x.OrganizationId)
            .NotEmpty().WithMessage("Organization ID is required.");

        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("First name is required.")
            .MaximumLength(100).WithMessage("First name must not exceed 100 characters.");

        RuleFor(x => x.MiddleName)
            .MaximumLength(100).WithMessage("Middle name must not exceed 100 characters.")
            .When(x => !string.IsNullOrWhiteSpace(x.MiddleName));

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Last name is required.")
            .MaximumLength(100).WithMessage("Last name must not exceed 100 characters.");

        RuleFor(x => x.PreferredName)
            .MaximumLength(100).WithMessage("Preferred name must not exceed 100 characters.")
            .When(x => !string.IsNullOrWhiteSpace(x.PreferredName));

        RuleFor(x => x.Phone)
            .NotEmpty().WithMessage("Phone number is required.")
            .MaximumLength(30).WithMessage("Phone number must not exceed 30 characters.");

        RuleFor(x => x.AlternatePhone)
            .MaximumLength(30).WithMessage("Alternate phone must not exceed 30 characters.")
            .When(x => !string.IsNullOrWhiteSpace(x.AlternatePhone));

        RuleFor(x => x.Email)
            .EmailAddress().WithMessage("Email format is invalid.")
            .MaximumLength(256).WithMessage("Email must not exceed 256 characters.")
            .When(x => !string.IsNullOrWhiteSpace(x.Email));

        RuleFor(x => x.NationalIdOrPassport)
            .MaximumLength(50).WithMessage("National ID or passport must not exceed 50 characters.")
            .When(x => !string.IsNullOrWhiteSpace(x.NationalIdOrPassport));

        RuleFor(x => x.Notes)
            .MaximumLength(2000).WithMessage("Notes must not exceed 2000 characters.")
            .When(x => !string.IsNullOrWhiteSpace(x.Notes));
    }
}
