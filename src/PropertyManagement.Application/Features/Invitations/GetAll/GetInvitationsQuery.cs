using MediatR;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Application.Features.Invitations.Responses;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Application.Features.Invitations.GetAll;

public sealed record GetInvitationsQuery(
    Guid OrganizationId,
    InvitationStatus? Status = null) : IRequest<Result<IReadOnlyList<InvitationResponse>>>;
