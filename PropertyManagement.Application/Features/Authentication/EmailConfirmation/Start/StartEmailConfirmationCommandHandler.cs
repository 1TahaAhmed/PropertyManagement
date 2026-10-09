using System.Text.Encodings.Web;
using MediatR;
using PropertyManagement.Application.Abstractions;
using PropertyManagement.Application.Abstractions.Email;
using PropertyManagement.Application.Abstractions.Identity;
using PropertyManagement.Application.Abstractions.Security;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Domain.Entities.Authentication;

namespace PropertyManagement.Application.Features.Authentication.EmailConfirmation.Start;

public sealed class StartEmailConfirmationCommandHandler(
    IIdentityService identityService,
    IEmailConfirmationChallengeRepository challengeRepository,
    IVerificationCodeGenerator codeGenerator,
    IVerificationCodeProtector codeProtector,
    IEmailSender emailSender,
    IClock clock,
    IEmailConfirmationSettings settings)
    : IRequestHandler<
        StartEmailConfirmationCommand,
        Result<StartEmailConfirmationResponse>>
{
    public async Task<Result<StartEmailConfirmationResponse>> Handle(
        StartEmailConfirmationCommand request,
        CancellationToken cancellationToken)
    {
        var identityResult =
            await identityService.ValidateEmailConfirmationTokenAsync(
                request.UserId,
                request.Token,
                cancellationToken);

        if (identityResult.IsFailure)
        {
            return Result<StartEmailConfirmationResponse>.Failure(
                identityResult.Errors.ToArray());
        }

        var user = identityResult.Value;
        var now = clock.UtcNow;

        var activeChallenge =
            await challengeRepository.GetActiveByUserIdAsync(
                user.UserId,
                now,
                cancellationToken);

        if (activeChallenge is not null)
        {
            activeChallenge.Revoke(
                revokedAt: now,
                reason: "Resent");
        }

        var verificationCode = codeGenerator.Generate(
            EmailConfirmationChallenge.CodeLength);

        var protectedCode = codeProtector.Protect(
            verificationCode);

        var expiresAt = now.AddMinutes(
            settings.CodeLifetimeMinutes);

        var challenge = EmailConfirmationChallenge.Create(
            userId: user.UserId,
            protectedCode: protectedCode,
            createdAt: now,
            expiresAt: expiresAt);

        await challengeRepository.AddAsync(
            challenge,
            cancellationToken);

        await challengeRepository.SaveChangesAsync(
            cancellationToken);

        var emailBody = BuildVerificationCodeEmailBody(
            user.FirstName,
            verificationCode,
            settings.CodeLifetimeMinutes);

        await emailSender.SendAsync(
            recipient: user.Email,
            subject: "Your email verification code",
            body: emailBody,
            cancellationToken);

        return Result<StartEmailConfirmationResponse>.Success(
            new StartEmailConfirmationResponse(
                Message:
                    "A verification code has been sent to your email address."));
    }

    private static string BuildVerificationCodeEmailBody(
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
                    If you did not request this code, you can ignore
                    this email.
                </p>
            </body>
            </html>
            """;
    }
}
