using System.Text.Encodings.Web;
using MediatR;
using PropertyManagement.Application.Abstractions.Email;
using PropertyManagement.Application.Abstractions.Identity;
using PropertyManagement.Application.Common.Results;

namespace PropertyManagement.Application.Features.Authentication.PasswordReset.ForgotPassword;

public sealed class ForgotPasswordCommandHandler(
    IIdentityService identityService,
    IEmailSender emailSender)
    : IRequestHandler<ForgotPasswordCommand, Result>
{
    public async Task<Result> Handle(
        ForgotPasswordCommand request,
        CancellationToken cancellationToken)
    {
        var tokenResult = await identityService.GeneratePasswordResetTokenAsync(
            request.Email,
            cancellationToken);

        if (tokenResult.IsSuccess)
        {
            var encodedToken = HtmlEncoder.Default.Encode(tokenResult.Value);
            var subject = "Reset Your Password";
            var body = $"""
                <!doctype html>
                <html lang="en">
                <head>
                    <meta charset="utf-8">
                    <title>Reset Your Password</title>
                </head>
                <body>
                    <p>Use the following token to reset your password:</p>
                    <p><code>{encodedToken}</code></p>
                    <p>If you did not request this, please ignore this email.</p>
                </body>
                </html>
                """;

            await emailSender.SendAsync(
                request.Email,
                subject,
                body,
                cancellationToken);
        }

        return Result.Success();
    }
}
