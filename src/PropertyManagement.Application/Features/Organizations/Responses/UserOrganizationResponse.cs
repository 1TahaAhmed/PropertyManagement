using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Application.Features.Organizations.Responses;

public sealed record UserOrganizationResponse(
    Guid OrganizationId,
    string Name,
    string Identifier,
    OrganizationStatus Status,
    IReadOnlyList<MembershipRole> Roles,
    bool IsCurrent);
