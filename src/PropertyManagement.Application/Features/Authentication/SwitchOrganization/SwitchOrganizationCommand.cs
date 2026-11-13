using MediatR;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Application.Features.Authentication.Login;

namespace PropertyManagement.Application.Features.Authentication.SwitchOrganization;

public sealed record SwitchOrganizationCommand(
    Guid UserId,
    Guid TargetOrganizationId,
    string? CurrentRefreshToken = null) : IRequest<Result<LoginResponse>>;
