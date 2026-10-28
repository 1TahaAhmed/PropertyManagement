using MediatR;
using PropertyManagement.Application.Abstractions.Email;
using PropertyManagement.Application.Abstractions.Identity;
using PropertyManagement.Application.Common.Results;

namespace PropertyManagement.Application.Features.Authentication.Registeration;

public sealed class RegisterCommandHandler(
    IIdentityService identityService,
    IEmailConfirmationCodeIssuer codeIssuer)
    : IRequestHandler<RegisterCommand, Result<RegisterResponse>>
{
    public async Task<Result<RegisterResponse>> Handle(
        RegisterCommand request,
        CancellationToken cancellationToken)
    {
        var identityResult = await identityService.RegisterAsync(
            request.FirstName,
            request.LastName,
            request.Address,
            request.Email,
            request.Password,
            phoneNumber: null,
            cancellationToken: cancellationToken);

        if (identityResult.IsFailure)
        {
            return Result<RegisterResponse>.Failure(
                identityResult.Errors.ToArray());
        }

        var identityUser = identityResult.Value;

        var codeIssueResult = await codeIssuer.IssueAsync(
            userId: identityUser.UserId,
            email: request.Email,
            firstName: request.FirstName,
            cancellationToken: cancellationToken);

        if (codeIssueResult.IsFailure)
        {
            await identityService.DeleteUserAsync(identityUser.UserId, CancellationToken.None);
            return Result<RegisterResponse>.Failure(
                codeIssueResult.Errors.ToArray());
        }

        var response = new RegisterResponse(
            UserId: identityUser.UserId,
            Email: request.Email,
            FirstName: request.FirstName,
            LastName: request.LastName,
            EmailConfirmed: false,
            CreatedAt: identityUser.CreatedAt);

        return Result<RegisterResponse>.Success(response);
    }
}
