using MediatR;
using PropertyManagement.Application.Abstractions.Organizations;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Application.Features.Organizations.Responses;

namespace PropertyManagement.Application.Features.Organizations.GetInvitations;

public sealed class GetOrganizationInvitationsQueryHandler(
    IOrganizationRepository organizationRepository)
    : IRequestHandler<GetOrganizationInvitationsQuery, Result<IReadOnlyList<InvitationResponse>>>
{
    public async Task<Result<IReadOnlyList<InvitationResponse>>> Handle(
        GetOrganizationInvitationsQuery query,
        CancellationToken cancellationToken)
    {
        var invitations = await organizationRepository.GetInvitationsByOrganizationAsync(
            query.OrganizationId,
            query.Status,
            cancellationToken);

        var response = invitations
            .Select(i => new InvitationResponse(
                Id: i.Id,
                OrganizationId: i.OrganizationId,
                Email: i.Email,
                Role: i.Role,
                Status: i.Status,
                ExpiresAtUtc: i.ExpiresAtUtc,
                CreatedAtUtc: i.CreatedAtUtc,
                RespondedAtUtc: i.RespondedAtUtc))
            .ToList();

        return Result<IReadOnlyList<InvitationResponse>>.Success(response);
    }
}
