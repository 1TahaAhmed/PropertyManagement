using System;
using System.Collections.Generic;
using System.Text;

namespace PropertyManagement.Application.Abstractions.Email
{
    public interface IEmailConfirmationSettings
    {
        int CodeLifetimeMinutes { get; }
    }
}
