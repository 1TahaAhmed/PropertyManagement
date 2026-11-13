using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Application.Features.Organizations.Responses;

public sealed record AcceptInvitationResponse(
    Guid OrganizationId,
    string OrganizationName,
    MembershipRole Role,
    DateTime JoinedAtUtc);
