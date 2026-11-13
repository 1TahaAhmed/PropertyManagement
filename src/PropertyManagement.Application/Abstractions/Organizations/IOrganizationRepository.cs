using PropertyManagement.Domain.Entities.Organizations;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Application.Abstractions.Organizations;

public interface IOrganizationRepository
{
    Task<Organization?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Organization?> GetByIdentifierAsync(string identifier, CancellationToken cancellationToken = default);
    Task<bool> ExistsByNameAsync(string name, CancellationToken cancellationToken = default);
    Task<bool> ExistsByIdentifierAsync(string identifier, CancellationToken cancellationToken = default);
    Task AddAsync(Organization organization, CancellationToken cancellationToken = default);
    Task AddMemberAsync(OrganizationMember member, CancellationToken cancellationToken = default);
    Task<OrganizationMember?> GetMemberAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken = default);
    Task<OrganizationMember?> GetMemberByIdAsync(Guid memberId, Guid organizationId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OrganizationMember>> GetMembersAsync(Guid organizationId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<(Organization Organization, List<MembershipRole> Roles)>> GetUserOrganizationsAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<int> CountActiveOwnersAsync(Guid organizationId, CancellationToken cancellationToken = default);

    Task AddInvitationAsync(Invitation invitation, CancellationToken cancellationToken = default);
    Task<Invitation?> GetInvitationByIdAsync(Guid invitationId, Guid organizationId, CancellationToken cancellationToken = default);
    Task<Invitation?> GetInvitationByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default);
    Task<Invitation?> GetPendingInvitationByEmailAsync(Guid organizationId, string email, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Invitation>> GetInvitationsByOrganizationAsync(Guid organizationId, InvitationStatus? status = null, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
