using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Application.Features.Organizations.Responses;

public sealed record OrganizationMemberResponse(
    Guid Id,
    Guid OrganizationId,
    Guid UserId,
    string? Email,
    string? FirstName,
    string? LastName,
    MembershipRole Role,
    MembershipStatus Status,
    DateTime JoinedAt,
    DateTime? UpdatedAt);
