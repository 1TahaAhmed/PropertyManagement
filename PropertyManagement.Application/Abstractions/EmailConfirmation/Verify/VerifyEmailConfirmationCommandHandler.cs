using MediatR;
using PropertyManagement.Application.Abstractions;
using PropertyManagement.Application.Abstractions.Email;
using PropertyManagement.Application.Abstractions.EmailConfirmation.Verify;
using PropertyManagement.Application.Abstractions.Identity;
using PropertyManagement.Application.Abstractions.Security;
using PropertyManagement.Application.Common.Enums;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Domain.Entities.Authentication;

namespace PropertyManagement.Application.Features.Authentication.EmailConfirmation.Verify;

public sealed class VerifyEmailConfirmationCommandHandler(
    IEmailConfirmationChallengeRepository challengeRepository,
    IVerificationCodeProtector codeProtector,
    IIdentityService identityService,
    IClock clock)
    : IRequestHandler<
        VerifyEmailConfirmationCommand,
        Result<VerifyEmailConfirmationResponse>>
{
    public async Task<Result<VerifyEmailConfirmationResponse>> Handle(
        VerifyEmailConfirmationCommand request,
        CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;

        var challenge =
            await challengeRepository.GetActiveByUserIdAsync(
                request.UserId,
                now,
                cancellationToken);

        if (challenge is null)
        {
            return Result<VerifyEmailConfirmationResponse>.Failure(
                new Error(
                    Code: "EmailConfirmation.ChallengeNotFound",
                    Message:
                        "The verification code is invalid or expired.",
                    Type: ErrorType.Validation));
        }

        var isCodeValid = codeProtector.Verify(
            challenge.ProtectedCode,
            request.Code);

        if (!isCodeValid)
        {
            challenge.RegisterFailedAttempt(now);

            await challengeRepository.SaveChangesAsync(
                cancellationToken);

            return Result<VerifyEmailConfirmationResponse>.Failure(
                new Error(
                    Code: "EmailConfirmation.InvalidCode",
                    Message: "The verification code is invalid.",
                    Type: ErrorType.Validation));
        }

        var confirmationResult =
            await identityService.ConfirmEmailAsync(
                request.UserId,
                cancellationToken);

        if (confirmationResult.IsFailure)
        {
            return Result<VerifyEmailConfirmationResponse>.Failure(
                confirmationResult.Errors.ToArray());
        }

        challenge.MarkAsUsed(now);

        await challengeRepository.SaveChangesAsync(
            cancellationToken);

        return Result<VerifyEmailConfirmationResponse>.Success(
            new VerifyEmailConfirmationResponse(
                Message: "Email confirmed successfully."));
    }
}
