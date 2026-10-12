using PropertyManagement.Application.Common.Results;
using System;
using System.Collections.Generic;
using System.Text;

namespace PropertyManagement.Application.Abstractions.Email
{
    public interface IEmailConfirmationCodeIssuer
    {
        Task<Result> IssueAsync(
            Guid userId,
            string email,
            string firstName,
            CancellationToken cancellationToken = default);
    }
}
