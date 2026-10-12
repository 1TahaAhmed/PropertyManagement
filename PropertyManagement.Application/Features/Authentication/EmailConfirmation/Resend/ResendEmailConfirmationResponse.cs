using System;
using System.Collections.Generic;
using System.Text;

namespace PropertyManagement.Application.Features.Authentication.EmailConfirmation.Resend
{
    public sealed record ResendEmailConfirmationResponse(
        string Message
    );
}
