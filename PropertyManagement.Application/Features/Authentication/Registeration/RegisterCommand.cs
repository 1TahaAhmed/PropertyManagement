using System;
using System.Collections.Generic;
using System.Text;

namespace PropertyManagement.Application.Features.Authentication.Register
{
    public sealed record RegisterCommand(
        string FirstName,
        string LastName,
        string Address,
        string Email,
        string Password,
        string ConfirmPassword
    );
}
