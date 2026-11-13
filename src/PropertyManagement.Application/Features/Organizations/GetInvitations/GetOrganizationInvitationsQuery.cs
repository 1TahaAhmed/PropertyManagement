using MediatR;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Application.Features.Organizations.Responses;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Application.Features.Organizations.GetInvitations;

public sealed record GetOrganizationInvitationsQuery(
    Guid OrganizationId,
    InvitationStatus? Status = null) : IRequest<Result<IReadOnlyList<InvitationResponse>>>;
