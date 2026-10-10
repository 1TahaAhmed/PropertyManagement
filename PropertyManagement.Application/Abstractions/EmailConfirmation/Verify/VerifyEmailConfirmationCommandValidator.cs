using FluentValidation;
using PropertyManagement.Domain.Entities.Authentication;
using System;
using System.Collections.Generic;
using System.Data;
using System.Text;

namespace PropertyManagement.Application.Abstractions.EmailConfirmation.Verify
{
    public sealed class VerifyEmailConfirmationCommandValidator
    : AbstractValidator<VerifyEmailConfirmationCommand>
    {
        public VerifyEmailConfirmationCommandValidator()
        {
            RuleFor(command => command.UserId)
                .NotEmpty()
                .WithErrorCode("UserIdRequired")
                .WithMessage("User ID is required.");

            RuleFor(command => command.Code)
                .NotEmpty()
                .WithErrorCode("CodeRequired")
                .WithMessage("Verification code is required.")
                .Length(EmailConfirmationChallenge.CodeLength)
                .WithErrorCode("InvalidCodeLength")
                .WithMessage(
                    $"Verification code must contain exactly " +
                    $"{EmailConfirmationChallenge.CodeLength} digits.")
                .Matches("^[0-9]+$")
                .WithErrorCode("InvalidCodeFormat")
                .WithMessage("Verification code must contain digits only.");
        }
    }
}
