using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Api.Contracts.Organizations;

public sealed record ChangeMemberRoleRequest(MembershipRole Role);
