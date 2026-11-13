using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Api.Contracts.Organizations;

public sealed record InviteMemberRequest(
    string Email,
    MembershipRole Role);
