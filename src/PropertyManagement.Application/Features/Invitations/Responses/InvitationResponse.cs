using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Application.Features.Invitations.Responses;

public sealed record InvitationResponse(
    Guid Id,
    Guid OrganizationId,
    string Email,
    MembershipRole Role,
    InvitationStatus Status,
    DateTime ExpiresAtUtc,
    DateTime CreatedAtUtc,
    DateTime? RespondedAtUtc);
