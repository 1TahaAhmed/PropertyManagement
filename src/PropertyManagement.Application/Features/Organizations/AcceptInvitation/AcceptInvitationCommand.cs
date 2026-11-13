using MediatR;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Application.Features.Organizations.Responses;

namespace PropertyManagement.Application.Features.Organizations.AcceptInvitation;

public sealed record AcceptInvitationCommand(
    Guid UserId,
    string? UserEmail,
    string Token) : IRequest<Result<AcceptInvitationResponse>>;
