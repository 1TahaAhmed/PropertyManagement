using MediatR;
using PropertyManagement.Application.Common.Results;

namespace PropertyManagement.Application.Features.Authentication.RevokeToken;

public sealed record RevokeTokenCommand(string RefreshToken) : IRequest<Result>;
