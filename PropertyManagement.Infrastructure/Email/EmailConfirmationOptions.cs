using System;
using System.Collections.Generic;
using System.Text;

namespace PropertyManagement.Infrastructure.Email
{
    public sealed class EmailConfirmationOptions
    {
        public const string SectionName = "Email:Confirmation";
        public string BaseUrl { get; init; } = string.Empty;
    }
}
