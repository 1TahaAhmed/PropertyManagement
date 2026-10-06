using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using PropertyManagement.Application.Abstractions;
using PropertyManagement.Application.Abstractions.Identity;
using PropertyManagement.Application.Common.Enums;
using PropertyManagement.Application.Common.Results;

namespace PropertyManagement.Infrastructure.Identity;

public sealed class IdentityService(
    UserManager<ApplicationUser> userManager,
    IClock clock) : IIdentityService
{

    private static ErrorType MapIdentityErrorType(string errorCode)
    {
        return errorCode switch
        {
            "DuplicateUserName" => ErrorType.Conflict,
            "DuplicateEmail" => ErrorType.Conflict,
            "InvalidUserName" => ErrorType.Validation,
            "InvalidEmail" => ErrorType.Validation,
            "PasswordTooShort" => ErrorType.Validation,
            "PasswordRequiresNonAlphanumeric" => ErrorType.Validation,
            "PasswordRequiresDigit" => ErrorType.Validation,
            "PasswordRequiresLower" => ErrorType.Validation,
            "PasswordRequiresUpper" => ErrorType.Validation,
            _ => ErrorType.Failure
        }; 
    }

    public async Task<Result<IdentityUserResult>> RegisterAsync(
        string firstName,
        string lastName,
        string address,
        string email,
        string password,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var createdAt = clock.UtcNow;

        var user = ApplicationUser.Create(
            firstName,
            lastName,
            address,
            email,
            createdAt);

        var identityResult = await userManager.CreateAsync(
            user,
            password);

        if (identityResult.Succeeded)
        {
            var identityUserResult = new IdentityUserResult(
                UserId: user.Id,
                CreatedAt: user.CreatedAt);

            return Result<IdentityUserResult>.Success(
                identityUserResult);
        }

        var errors = identityResult.Errors
            .Select(error => new Error(
                Code: $"Identity.{error.Code}",
                Message: error.Description,
                Type: MapIdentityErrorType(error.Code)))
            .ToArray();

        return Result<IdentityUserResult>.Failure(errors);
    }
}
