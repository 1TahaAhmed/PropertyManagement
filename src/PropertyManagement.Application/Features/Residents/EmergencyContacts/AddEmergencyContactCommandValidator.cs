using FluentValidation;

namespace PropertyManagement.Application.Features.Residents.EmergencyContacts;

public sealed class AddEmergencyContactCommandValidator : AbstractValidator<AddEmergencyContactCommand>
{
    public AddEmergencyContactCommandValidator()
    {
        RuleFor(x => x.ResidentId)
            .NotEmpty()
            .WithMessage("Resident ID is required.");

        RuleFor(x => x.OrganizationId)
            .NotEmpty()
            .WithMessage("Organization ID is required.");

        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Contact name is required.")
            .MaximumLength(150)
            .WithMessage("Contact name must not exceed 150 characters.");

        RuleFor(x => x.Relationship)
            .NotEmpty()
            .WithMessage("Relationship is required.")
            .MaximumLength(50)
            .WithMessage("Relationship must not exceed 50 characters.");

        RuleFor(x => x.Phone)
            .NotEmpty()
            .WithMessage("Phone number is required.")
            .MaximumLength(30)
            .WithMessage("Phone number must not exceed 30 characters.");

        RuleFor(x => x.Email)
            .EmailAddress()
            .WithMessage("Email format is invalid.")
            .MaximumLength(256)
            .WithMessage("Email must not exceed 256 characters.")
            .When(x => !string.IsNullOrWhiteSpace(x.Email));

        RuleFor(x => x.Notes)
            .MaximumLength(500)
            .WithMessage("Notes must not exceed 500 characters.")
            .When(x => !string.IsNullOrWhiteSpace(x.Notes));
    }
}
