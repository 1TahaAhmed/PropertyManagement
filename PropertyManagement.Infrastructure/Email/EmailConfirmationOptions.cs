using PropertyManagement.Application.Abstractions.Email;
using System;
using System.Collections.Generic;
using System.Text;

namespace PropertyManagement.Infrastructure.Email
{
    public sealed class EmailConfirmationOptions
        : IEmailConfirmationSettings
    {
        public const string SectionName = "Email:Confirmation";
        public string BaseUrl { get; init; } = string.Empty;
        public int CodeLifetimeMinutes { get; init; } = 10;
    }
}
