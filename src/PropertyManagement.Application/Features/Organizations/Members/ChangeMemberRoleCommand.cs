using MediatR;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Application.Features.Organizations.Members;

public sealed record ChangeMemberRoleCommand(
    Guid OrganizationId,
    Guid MemberId,
    MembershipRole NewRole,
    Guid ActorUserId,
    MembershipRole ActorRole) : IRequest<Result>;
