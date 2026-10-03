using PropertyManagement.Application.Common.Results;
using System;
using System.Collections.Generic;
using System.Text;

namespace PropertyManagement.Application.Abstractions.Identity
{
    public interface IIdentityService
    {
        Task<Result<string>> RegisterAsync(
            string firstName,
            string lastName,
            string address,
            string email,
            string password,
            CancellationToken cancellationToken = default
        );
    }
}
