using PropertyManagement.Application.Common.Results;
using System;
using System.Collections.Generic;
using System.Text;

namespace PropertyManagement.Application.Abstractions.Identity
{
    public interface IIdentityService
    {
        Task<Result<IdentityUserResult>> RegisterAsync(
            string firstName,
            string lastName,
            string address,
            string email,
            string password,
            CancellationToken cancellationToken = default
        );

        Task<Result<string>> GenerateEmailConfirmationTokenAsync(
            Guid userId,
            CancellationToken cancellationToken = default
        );

        Task<Result<EmailConfirmationUserResult>> ValidateEmailConfirmationTokenAsync(
            Guid userId,
            string token,
            CancellationToken cancellationToken = default);

        Task<Result> ConfirmEmailAsync(
            Guid userId,
            CancellationToken cancellationToken = default);

        Task<Result<EmailConfirmationUserResult>> GetEmailConfirmationUserAsync(
            Guid userId,
            CancellationToken cancellationToken = default);

        Task<bool> ExistsAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

        Task<Result<string>> GeneratePasswordResetTokenAsync(
            string email,
            CancellationToken cancellationToken = default
        );

        Task<Result> ResetPasswordAsync(
            string email,
            string token,
            string newPassowrd,
            CancellationToken cancellationToken = default
        );
    }
}
