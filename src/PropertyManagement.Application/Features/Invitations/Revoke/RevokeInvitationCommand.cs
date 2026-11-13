using MediatR;
using PropertyManagement.Application.Common.Results;

namespace PropertyManagement.Application.Features.Invitations.Revoke;

public sealed record RevokeInvitationCommand(
    Guid OrganizationId,
    Guid InvitationId) : IRequest<Result>;
