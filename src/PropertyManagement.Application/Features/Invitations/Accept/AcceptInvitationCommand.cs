using MediatR;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Application.Features.Invitations.Responses;

namespace PropertyManagement.Application.Features.Invitations.Accept;

public sealed record AcceptInvitationCommand(
    Guid UserId,
    string? UserEmail,
    string Token) : IRequest<Result<AcceptInvitationResponse>>;
