using MediatR;
using PropertyManagement.Application.Abstractions.Organizations;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Application.Features.Invitations.Responses;

namespace PropertyManagement.Application.Features.Invitations.GetAll;

public sealed class GetInvitationsQueryHandler(
    IOrganizationRepository organizationRepository)
    : IRequestHandler<GetInvitationsQuery, Result<IReadOnlyList<InvitationResponse>>>
{
    public async Task<Result<IReadOnlyList<InvitationResponse>>> Handle(
        GetInvitationsQuery query,
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
