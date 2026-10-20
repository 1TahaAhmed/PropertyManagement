using FluentValidation;
using System;
using System.Collections.Generic;
using System.Text;

namespace PropertyManagement.Application.Features.Properties.Create
{
    public sealed class PropertyValidator : AbstractValidator<CreatePropertyCommand>
    {
        public PropertyValidator()
        {
            RuleFor(c => c.TenantId)
                .NotEmpty()
                .WithMessage("Tenant ID is required.");

            RuleFor(c => c.Name)
                .NotEmpty()
                .MaximumLength(150);

            RuleFor(c => c.Address)
                .NotEmpty()
                .MaximumLength(300);

            RuleFor(c => c.Type)
                .IsInEnum();

            RuleFor(c => c.Description)
                .MaximumLength(1000);
        }
    }
}
