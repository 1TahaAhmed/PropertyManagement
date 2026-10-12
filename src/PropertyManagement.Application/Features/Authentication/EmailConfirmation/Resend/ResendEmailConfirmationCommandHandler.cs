using MediatR;
using PropertyManagement.Application.Abstractions;
using PropertyManagement.Application.Abstractions.Email;
using PropertyManagement.Application.Abstractions.Identity;
using PropertyManagement.Application.Common.Enums;
using PropertyManagement.Application.Common.Results;

namespace PropertyManagement.Application.Features.Authentication.EmailConfirmation.Resend;

public sealed class ResendEmailConfirmationCommandHandler(
    IIdentityService identityService,
    IEmailConfirmationResendPolicy resendPolicy,
    IEmailConfirmationCodeIssuer codeIssuer,
    IClock clock)
    : IRequestHandler<
        ResendEmailConfirmationCommand,
        Result<ResendEmailConfirmationResponse>>
{
    public async Task<Result<ResendEmailConfirmationResponse>> Handle(
        ResendEmailConfirmationCommand request,
        CancellationToken cancellationToken)
    {
        var userResult =
            await identityService.GetEmailConfirmationUserAsync(
                request.UserId,
                cancellationToken);

        if (userResult.IsFailure)
        {
            return Result<ResendEmailConfirmationResponse>.Failure(
                new Error(
                    Code: "EmailConfirmation.ResendUnavailable",
                    Message:
                        "If the account is eligible, a new " +
                        "verification code will be sent.",
                    Type: ErrorType.Validation));
        }

        var user = userResult.Value;

        var now = clock.UtcNow;

        var rateLimitResult =
            await resendPolicy.TryAcquireAsync(
                user.UserId,
                request.IpAddress,
                now,
                cancellationToken);

        if (rateLimitResult.IsFailure)
        {
            return Result<ResendEmailConfirmationResponse>.Failure(
                rateLimitResult.Errors.ToArray());
        }

        var issueResult = await codeIssuer.IssueAsync(
            userId: user.UserId,
            email: user.Email,
            firstName: user.FirstName,
            cancellationToken: cancellationToken);

        if (issueResult.IsFailure)
        {
            return Result<ResendEmailConfirmationResponse>.Failure(
                issueResult.Errors.ToArray());
        }

        return Result<ResendEmailConfirmationResponse>.Success(
            new ResendEmailConfirmationResponse(
                Message:
                    "If the account is eligible, a new " +
                    "verification code has been sent."));
    }
}
