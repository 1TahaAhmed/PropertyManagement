using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
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
        string? phoneNumber = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var createdAt = clock.UtcNow;

        var user = ApplicationUser.Create(
            firstName,
            lastName,
            address,
            email,
            createdAt,
            phoneNumber);

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

    public async Task<Result<string>> GenerateEmailConfirmationTokenAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var user = await userManager.FindByIdAsync(
            userId.ToString());

        if (user is null)
        {
            return Result<string>.Failure(
                new Error(
                    Code: "Identity.UserNotFound",
                    Message: "The user was not found.",
                    Type: ErrorType.NotFound));
        }

        if (user.EmailConfirmed)
        {
            return Result<string>.Failure(
                new Error(
                    Code: "Identity.EmailAlreadyConfirmed",
                    Message: "The email address is already confirmed.",
                    Type: ErrorType.Failure));
        }

        var token = await userManager.GenerateEmailConfirmationTokenAsync(user);

        return Result<string>.Success(token);
    }

    public async Task<Result<EmailConfirmationUserResult>>
        ValidateEmailConfirmationTokenAsync(
        Guid userId,
        string token,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var user = await userManager.FindByIdAsync(
            userId.ToString());

        if (user is null)
        {
            return Result<EmailConfirmationUserResult>.Failure(
                new Error(
                    Code: "Identity.UserNotFound",
                    Message: "The user was not found.",
                    Type: ErrorType.NotFound));
        }

        if (user.EmailConfirmed)
        {
            return Result<EmailConfirmationUserResult>.Failure(
                new Error(
                    Code: "Identity.EmailAlreadyConfirmed",
                    Message: "The email address is already confirmed.",
                    Type: ErrorType.Failure));
        }

        var isValid = await userManager.VerifyUserTokenAsync(
            user,
            userManager.Options.Tokens
                .EmailConfirmationTokenProvider,
            UserManager<ApplicationUser>
                .ConfirmEmailTokenPurpose,
            token);

        if (!isValid)
        {
            return Result<EmailConfirmationUserResult>.Failure(
                new Error(
                    Code: "Identity.InvalidEmailConfirmationToken",
                    Message: "The confirmation link is invalid or expired.",
                    Type: ErrorType.Validation));
        }

        return Result<EmailConfirmationUserResult>.Success(
            new EmailConfirmationUserResult(
                UserId: user.Id,
                Email: user.Email!,
                FirstName: user.FirstName));
    }

    public async Task<Result> ConfirmEmailAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var user = await userManager.FindByIdAsync(
            userId.ToString());

        if (user is null)
        {
            return Result.Failure(
                new Error(
                    Code: "Identity.UserNotFound",
                    Message: "The user was not found.",
                    Type: ErrorType.NotFound));
        }

        if (user.EmailConfirmed)
        {
            return Result.Failure(
                new Error(
                    Code: "Identity.EmailAlreadyConfirmed",
                    Message: "The email address is already confirmed.",
                    Type: ErrorType.Failure));
        }

        var token = await userManager.GenerateEmailConfirmationTokenAsync(
            user);

        var identityResult = await userManager.ConfirmEmailAsync(
            user,
            token);

        if (!identityResult.Succeeded)
        {
            var errors = identityResult.Errors
                .Select(error => new Error(
                    Code: $"Identity.{error.Code}",
                    Message: error.Description,
                    Type: ErrorType.Failure))
                .ToArray();

            return Result.Failure(errors);
        }

        return Result.Success();
    }

    public async Task<Result<EmailConfirmationUserResult>>
        GetEmailConfirmationUserAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var user = await userManager.FindByIdAsync(
                userId.ToString());

            if (user is null)
            {
                return Result<EmailConfirmationUserResult>.Failure(
                    new Error(
                        Code: "Identity.UserNotFound",
                        Message: "The user was not found.",
                        Type: ErrorType.NotFound));
            }

            if (user.EmailConfirmed)
            {
                return Result<EmailConfirmationUserResult>.Failure(
                    new Error(
                        Code: "Identity.EmailAlreadyConfirmed",
                        Message: "The email address is already confirmed.",
                        Type: ErrorType.Conflict));
            }
            return Result<EmailConfirmationUserResult>.Success(
                new EmailConfirmationUserResult(
                    UserId: user.Id,
                    Email: user.Email!,
                    FirstName: user.FirstName));
    }

    public async Task<bool> ExistsAsync(Guid userId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return await userManager.Users
        .AnyAsync(u => u.Id == userId && u.EmailConfirmed, cancellationToken);
    }

    public async Task<Result<string>> GeneratePasswordResetTokenAsync(
        string email,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            return Result<string>.Failure(
                new Error(
                    Code: "Identity.UserNotFound",
                    Message: "The user was not found.",
                    Type: ErrorType.NotFound));
        }

        if (!user.EmailConfirmed)
        {
            return Result<string>.Failure(
                new Error(
                    Code: "Identity.EmailNotConfirmed",
                    Message: "The email address is not confirmed.",
                    Type: ErrorType.Failure));
        }

        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        return Result<string>.Success(token);
    }

    public async Task<Result> ResetPasswordAsync(
        string email,
        string token,
        string newPassword,
        CancellationToken cancellationToken = default)
    {   
        cancellationToken.ThrowIfCancellationRequested();

        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            return Result.Failure(
                new Error(
                    Code: "Identity.InvalidResetToken",
                    Message: "Invalid email or reset token.",
                    Type: ErrorType.Validation));
        }

        var identityResult = await userManager.ResetPasswordAsync(user, token, newPassword);
        if (identityResult.Succeeded)
        {
            return Result.Success();
        }

        var errors = identityResult.Errors
            .Select(error => new Error(
                Code: $"Identity.{error.Code}",
                Message: error.Description,
                Type: MapIdentityErrorType(error.Code)))
            .ToArray();

        return Result.Failure(errors);
    }

    public async Task<Result> DeleteUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return Result.Success();
        }

        var identityResult = await userManager.DeleteAsync(user);
        if (!identityResult.Succeeded)
        {
            var errors = identityResult.Errors
                .Select(error => new Error(
                    Code: $"Identity.{error.Code}",
                    Message: error.Description,
                    Type: ErrorType.Failure))
                .ToArray();

            return Result.Failure(errors);
        }

        return Result.Success();
    }
}
