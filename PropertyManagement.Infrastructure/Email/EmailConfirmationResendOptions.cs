using PropertyManagement.Application.Abstractions.Email;
using System;
using System.Collections.Generic;
using System.Text;

namespace PropertyManagement.Infrastructure.Email
{
    public sealed class EmailConfirmationResendOptions
        : IEmailConfirmationResendSettings
    {
        public const string SectionName =
            "Email:Confirmation:Resend";
        public int CooldownSeconds { get; init; } = 60;

        public int WindowMinutes { get; init; } = 15;

        public int MaxRequestsPerWindow { get; init; } = 5;

        public int MaxRequestsPerDay { get; init; } = 20;
    }
}
