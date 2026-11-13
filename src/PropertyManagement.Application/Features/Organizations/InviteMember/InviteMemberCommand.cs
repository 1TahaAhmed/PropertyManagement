using MediatR;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Application.Features.Organizations.Responses;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Application.Features.Organizations.InviteMember;

public sealed record InviteMemberCommand(
    Guid OrganizationId,
    string Email,
    MembershipRole Role) : IRequest<Result<InvitationResponse>>;
