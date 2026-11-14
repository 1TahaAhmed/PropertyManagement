using MediatR;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Application.Features.Organizations.Members;

public sealed record DeactivateMemberCommand(
    Guid OrganizationId,
    Guid MemberId,
    Guid ActorUserId,
    MembershipRole ActorRole) : IRequest<Result>;
