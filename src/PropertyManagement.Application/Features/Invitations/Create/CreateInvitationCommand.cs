using MediatR;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Application.Features.Invitations.Responses;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Application.Features.Invitations.Create;

public sealed record CreateInvitationCommand(
    Guid OrganizationId,
    string Email,
    MembershipRole Role) : IRequest<Result<InvitationResponse>>;
