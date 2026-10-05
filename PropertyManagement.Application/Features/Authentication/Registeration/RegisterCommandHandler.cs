using MediatR;
using PropertyManagement.Application.Abstractions.Identity;
using PropertyManagement.Application.Common.Results;

namespace PropertyManagement.Application.Features.Authentication.Registeration
{
    public sealed class RegisterCommandHandler(
        IIdentityService identityService)
       : IRequestHandler<RegisterCommand, Result<RegisterResponse>>
    {
        public async Task<Result<RegisterResponse>> Handle(RegisterCommand request, CancellationToken cancellationToken)
        {
            var identityResult = await identityService.RegisterAsync(
                request.FirstName,
                request.LastName,
                request.Address,
                request.Email,
                request.Password,
                cancellationToken);

            if (identityResult.IsFailure)
            {
                return Result<RegisterResponse>.Failure(identityResult.Errors.ToArray());
            }

            var response = new RegisterResponse(
                UserId: identityResult.Value,
                Email: request.Email,
                FirstName: request.FirstName,
                LastName: request.LastName,
                EmailConfirmed: false,
                CreatedAt: DateTime.UtcNow
            );

            return Result<RegisterResponse>.Success(response);
        }
    }
}
