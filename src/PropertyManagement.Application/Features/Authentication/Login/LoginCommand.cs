using MediatR;
using PropertyManagement.Application.Common.Results;

namespace PropertyManagement.Application.Features.Authentication.Login;

public sealed record LoginCommand(
    string Email,
    string Password,
    Guid? OrganizationId = null) : IRequest<Result<LoginResponse>>;
