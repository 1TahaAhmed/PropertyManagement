using System;
using System.Collections.Generic;
using System.Text;

namespace PropertyManagement.Application.Abstractions.Email
{
    public interface IEmailConfirmationResendSettings
    {
        int CooldownSeconds { get; }
        int WindowMinutes { get; }
        int MaxRequestsPerWindow { get; }
        int MaxRequestsPerDay { get; }
    }
}
