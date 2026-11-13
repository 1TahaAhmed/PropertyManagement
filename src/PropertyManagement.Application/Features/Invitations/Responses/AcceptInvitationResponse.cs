using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Application.Features.Invitations.Responses;

public sealed record AcceptInvitationResponse(
    Guid OrganizationId,
    string OrganizationName,
    MembershipRole Role,
    DateTime JoinedAtUtc);
