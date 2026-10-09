using MediatR;
using PropertyManagement.Application.Common.Results;

namespace PropertyManagement.Application.Features.Authentication.EmailConfirmation.Start
{
    public sealed record StartEmailConfirmationCommand(
    Guid UserId,
    string Token) : IRequest<Result<StartEmailConfirmationResponse>>;
}
