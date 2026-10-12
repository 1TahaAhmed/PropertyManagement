using System;
using System.Collections.Generic;
using System.Text;

namespace PropertyManagement.Application.Abstractions.Identity
{
    public sealed record IdentityUserResult(
        Guid UserId,
        DateTime CreatedAt
    );
}
