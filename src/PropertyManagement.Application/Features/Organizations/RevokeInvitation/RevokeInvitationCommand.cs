using MediatR;
using PropertyManagement.Application.Common.Results;

namespace PropertyManagement.Application.Features.Organizations.RevokeInvitation;

public sealed record RevokeInvitationCommand(
    Guid OrganizationId,
    Guid InvitationId) : IRequest<Result>;
