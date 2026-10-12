using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PropertyManagement.Application.Abstractions.Email;
using PropertyManagement.Application.Common.Enums;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Domain.Entities.Authentication;
using PropertyManagement.Infrastructure.Email;

namespace PropertyManagement.Infrastructure.Persistence.Repositories;

public sealed class EmailConfirmationResendPolicy(
    ApplicationDbContext dbContext,
    IOptions<EmailConfirmationResendOptions> options)
    : IEmailConfirmationResendPolicy
{
    private readonly EmailConfirmationResendOptions settings =
        options.Value;

    public async Task<Result> TryAcquireAsync(
        Guid userId,
        string ipAddress,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

        var cooldownStart = utcNow.AddSeconds(
            -settings.CooldownSeconds);

        var windowStart = utcNow.AddMinutes(
            -settings.WindowMinutes);

        var dayStart = utcNow.AddDays(-1);

        var mostRecentUserAttempt =
            await dbContext.EmailConfirmationResendAttempts
                .Where(attempt =>
                    attempt.UserId == userId)
                .OrderByDescending(attempt =>
                    attempt.RequestedAt)
                .Select(attempt =>
                    (DateTime?)attempt.RequestedAt)
                .FirstOrDefaultAsync(cancellationToken);

        if (mostRecentUserAttempt.HasValue &&
            mostRecentUserAttempt.Value >= cooldownStart)
        {
            return RateLimited(
                "Please wait before requesting another code.");
        }

        var userWindowCount =
            await dbContext.EmailConfirmationResendAttempts
                .CountAsync(
                    attempt =>
                        attempt.UserId == userId &&
                        attempt.RequestedAt >= windowStart,
                    cancellationToken);

        if (userWindowCount >=
            settings.MaxRequestsPerWindow)
        {
            return RateLimited(
                "The resend limit has been reached. " +
                "Please try again later.");
        }

        var userDayCount =
            await dbContext.EmailConfirmationResendAttempts
                .CountAsync(
                    attempt =>
                        attempt.UserId == userId &&
                        attempt.RequestedAt >= dayStart,
                    cancellationToken);

        if (userDayCount >= settings.MaxRequestsPerDay)
        {
            return RateLimited(
                "The daily resend limit has been reached. " +
                "Please try again later.");
        }

        var ipWindowCount =
            await dbContext.EmailConfirmationResendAttempts
                .CountAsync(
                    attempt =>
                        attempt.IpAddress == ipAddress &&
                        attempt.RequestedAt >= windowStart,
                    cancellationToken);

        if (ipWindowCount >=
            settings.MaxRequestsPerWindow)
        {
            return RateLimited(
                "The resend limit has been reached. " +
                "Please try again later.");
        }

        var resendAttempt =
            EmailConfirmationResendAttempt.Create(
                userId: userId,
                ipAddress: ipAddress,
                requestedAt: utcNow);

        await dbContext.EmailConfirmationResendAttempts.AddAsync(
            resendAttempt,
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return Result.Success();
    }

    private static Result RateLimited(string message)
    {
        return Result.Failure(
            new Error(
                Code: "EmailConfirmation.ResendRateLimited",
                Message: message,
                Type: ErrorType.TooManyRequests));
    }
}
