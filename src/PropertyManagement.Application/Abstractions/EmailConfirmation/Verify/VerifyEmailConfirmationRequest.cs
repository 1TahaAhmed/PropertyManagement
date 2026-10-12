using System;
using System.Collections.Generic;
using System.Text;

namespace PropertyManagement.Application.Abstractions.EmailConfirmation.Verify
{
    public sealed record VerifyEmailConfirmationRequest(
        Guid UserId,
        string Code);
}
