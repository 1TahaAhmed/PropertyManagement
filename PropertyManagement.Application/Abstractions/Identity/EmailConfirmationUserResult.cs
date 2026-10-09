using System;
using System.Collections.Generic;
using System.Text;

namespace PropertyManagement.Application.Abstractions.Identity
{
    public sealed record EmailConfirmationUserResult(
        Guid UserId,
        string Email,
        string FirstName);
}
