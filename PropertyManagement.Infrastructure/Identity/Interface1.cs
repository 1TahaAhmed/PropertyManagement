using Microsoft.AspNetCore.Identity;
using PropertyManagement.Application.Abstractions.Identity;
using PropertyManagement.Application.Common.Results;
using System;
using System.Collections.Generic;
using System.Text;

namespace PropertyManagement.Infrastructure.Identity
{
    public sealed class IdentityService(
        UserManager<ApplicationUser> userManager
        ) : IIdentityService
    {
        public async Task<Result<string>> RegisterAsync(
            string firstName,
            string lastName,
            string address,
            string email,
            string password,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var user = ApplicationUser.Create(firstName,
                lastName,
                address,
                email);

            var identityResult = await userManager.CreateAsync(
                user,
                password
            );

            if (identityResult.Succeeded)
            {
                return Result<string>.Success(user.Id);
            }

            var errors = identityResult.Errors
                .Select(error => new Error(
                    Code: error.Code,
                    Message: error.Description
                ))
                .ToArray();
            return Result<string>.Failure(errors);
        }
    }
}
