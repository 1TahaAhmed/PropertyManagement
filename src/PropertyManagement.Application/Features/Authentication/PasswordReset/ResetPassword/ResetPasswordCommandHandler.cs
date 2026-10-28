using MediatR;
using PropertyManagement.Application.Abstractions.Identity;
using PropertyManagement.Application.Common.Results;

namespace PropertyManagement.Application.Features.Authentication.PasswordReset.ResetPassword;

public sealed class ResetPasswordCommandHandler(
    IIdentityService identityService)
    : IRequestHandler<ResetPasswordCommand, Result>
{
    public async Task<Result> Handle(
        ResetPasswordCommand request,
        CancellationToken cancellationToken)
    {
        return await identityService.ResetPasswordAsync(
            request.Email,
            request.Token,
            request.NewPassword,
            cancellationToken);
    }
}