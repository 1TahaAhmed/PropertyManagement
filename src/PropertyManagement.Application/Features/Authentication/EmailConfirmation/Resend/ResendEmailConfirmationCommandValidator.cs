using FluentValidation;
using System;
using System.Collections.Generic;
using System.Text;

namespace PropertyManagement.Application.Features.Authentication.EmailConfirmation.Resend
{
    public class ResendEmailConfirmationCommandValidator
        : AbstractValidator<ResendEmailConfirmationCommand>
    {
        public ResendEmailConfirmationCommandValidator()
        {
            RuleFor(command => command.UserId)
           .NotEmpty()
           .WithErrorCode("UserIdRequired")
           .WithMessage("User ID is required.");

            RuleFor(command => command.IpAddress)
             .NotEmpty()
             .WithErrorCode("IpAddressRequired")
             .WithMessage("Client IP address is required.")
             .MaximumLength(64)
             .WithErrorCode("IpAddressTooLong")
             .WithMessage("Client IP address is invalid.");
        }
    }
}
