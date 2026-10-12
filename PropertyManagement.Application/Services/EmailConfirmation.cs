using System.Text.Encodings.Web;
using PropertyManagement.Application.Abstractions;
using PropertyManagement.Application.Abstractions.Email;
using PropertyManagement.Application.Abstractions.Security;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Domain.Entities.Authentication;

namespace PropertyManagement.Application.Services.EmailConfirmation;

public sealed class EmailConfirmationCodeIssuer(
    IEmailConfirmationChallengeRepository challengeRepository,
    IVerificationCodeGenerator codeGenerator,
    IVerificationCodeProtector codeProtector,
    IEmailSender emailSender,
    IClock clock,
    IEmailConfirmationSettings settings)
    : IEmailConfirmationCodeIssuer
{
    public async Task<Result> IssueAsync(
        Guid userId,
        string email,
        string firstName,
        CancellationToken cancellationToken = default)
    {
        var now = clock.UtcNow;

        var activeChallenge =
            await challengeRepository.GetActiveByUserIdAsync(
                userId,
                now,
                cancellationToken);

        if (activeChallenge is not null)
        {
            activeChallenge.Revoke(
                revokedAt: now,
                reason: "NewCodeIssued");
        }

        var verificationCode = codeGenerator.Generate(
            EmailConfirmationChallenge.CodeLength);

        var protectedCode = codeProtector.Protect(
            verificationCode);

        var expiresAt = now.AddMinutes(
            settings.CodeLifetimeMinutes);

        var challenge = EmailConfirmationChallenge.Create(
            userId: userId,
            protectedCode: protectedCode,
            createdAt: now,
            expiresAt: expiresAt);

        await challengeRepository.AddAsync(
            challenge,
            cancellationToken);

        await challengeRepository.SaveChangesAsync(
            cancellationToken);

        var emailBody = BuildEmailBody(
            firstName,
            verificationCode,
            settings.CodeLifetimeMinutes);

        await emailSender.SendAsync(
            recipient: email,
            subject: "Your email verification code",
            body: emailBody,
            cancellationToken);

        return Result.Success();
    }

    private static string BuildEmailBody(
        string firstName,
        string verificationCode,
        int lifetimeMinutes)
    {
        var encodedFirstName =
            HtmlEncoder.Default.Encode(firstName);

        var encodedVerificationCode =
            HtmlEncoder.Default.Encode(verificationCode);

        return $"""
            <!doctype html>
            <html lang="en">
            <head>
                <meta charset="utf-8">
                <title>Email verification code</title>
            </head>
            <body>
                <h2>Hello, {encodedFirstName}</h2>

                <p>
                    Your email verification code is:
                </p>

                <p>
                    <strong>{encodedVerificationCode}</strong>
                </p>

                <p>
                    This code expires in {lifetimeMinutes} minutes.
                </p>

                <p>
                    If you did not request this code,
                    you can ignore this email.
                </p>
            </body>
            </html>
            """;
    }
}
