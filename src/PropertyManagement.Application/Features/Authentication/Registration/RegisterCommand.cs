using MediatR;
using PropertyManagement.Application.Common.Results;

namespace PropertyManagement.Application.Features.Authentication.Registeration
{
    public sealed record RegisterCommand(
        string FirstName,
        string LastName,
        string Address,
        string Email,
        string Password,
        string ConfirmPassword
    ) : IRequest<Result<RegisterResponse>>;
}
