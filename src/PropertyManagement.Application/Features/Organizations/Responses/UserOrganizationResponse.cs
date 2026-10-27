using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Application.Features.Organizations.Response;

public sealed record UserOrganizationResponse(
    Guid OrganizationId,
    string OrganizationName,
    string OrganizationIdentifier,
    OrganizationStatus Status,
    IReadOnlyList<MembershipRole> Roles,
    DateTime JoinedAt);