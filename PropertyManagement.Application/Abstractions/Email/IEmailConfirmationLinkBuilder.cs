using System;
using System.Collections.Generic;
using System.Text;

namespace PropertyManagement.Application.Abstractions.Email
{
    public interface IEmailConfirmationLinkBuilder
    {
        string Build(
            Guid userId,
            string confirmationToken
        );
    }
}
