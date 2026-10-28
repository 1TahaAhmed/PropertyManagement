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
            var resetToken = tokenResult.Value;
            var subject = "Reset Your Password";
            var body = $"Use the following token to reset your password:\n\n{resetToken}\n\nIf you did not request this, please ignore this email.";

            await emailSender.SendAsync(
                request.Email,
                subject,
                body,
                cancellationToken);
        }

        return Result.Success();
    }
}