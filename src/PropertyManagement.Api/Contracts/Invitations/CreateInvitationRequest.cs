using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Api.Contracts.Invitations;

public sealed record CreateInvitationRequest(
    string Email,
    MembershipRole Role);
