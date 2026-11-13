using Microsoft.EntityFrameworkCore;
using PropertyManagement.Application.Abstractions.Organizations;
using PropertyManagement.Domain.Entities.Organizations;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Infrastructure.Persistence.Repositories;

public sealed class OrganizationRepository(ApplicationDbContext context) : IOrganizationRepository
{
    public async Task<Organization?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await context.Organizations
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
    }

    public async Task<Organization?> GetByIdentifierAsync(string identifier, CancellationToken cancellationToken = default)
    {
        return await context.Organizations
            .FirstOrDefaultAsync(o => o.Identifier == identifier, cancellationToken);
    }

    public async Task<bool> ExistsByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        return await context.Organizations
            .AnyAsync(o => o.Name == name, cancellationToken);
    }

    public async Task<bool> ExistsByIdentifierAsync(string identifier, CancellationToken cancellationToken = default)
    {
        return await context.Organizations
            .AnyAsync(o => o.Identifier == identifier, cancellationToken);
    }

    public async Task AddAsync(Organization organization, CancellationToken cancellationToken = default)
    {
        await context.Organizations.AddAsync(organization, cancellationToken);
    }

    public async Task AddMemberAsync(OrganizationMember member, CancellationToken cancellationToken = default)
    {
        await context.OrganizationMembers.AddAsync(member, cancellationToken);
    }

    public async Task<OrganizationMember?> GetMemberAsync(
        Guid organizationId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await context.OrganizationMembers
            .FirstOrDefaultAsync(m => m.OrganizationId == organizationId && m.UserId == userId, cancellationToken);
    }

    public async Task<IReadOnlyList<OrganizationMember>> GetMembersAsync(Guid organizationId, CancellationToken cancellationToken = default)
    {
        return await context.OrganizationMembers
            .AsNoTracking()
            .Where(m => m.OrganizationId == organizationId)
            .OrderBy(m => m.JoinedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<(Organization Organization, List<MembershipRole> Roles)>> GetUserOrganizationsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var memberships = await context.OrganizationMembers
            .AsNoTracking()
            .Include(m => m.Organization)
            .Where(m => m.UserId == userId
                     && m.Status == MembershipStatus.Active
                     && m.Organization.Status == OrganizationStatus.Active)
            .ToListAsync(cancellationToken);

        return memberships
            .GroupBy(m => m.Organization)
            .Select(g => (
                Organization: g.Key,
                Roles: g.Select(m => m.Role).Distinct().ToList()
            ))
            .ToList();
    }

    public async Task<int> CountActiveOwnersAsync(Guid organizationId, CancellationToken cancellationToken = default)
    {
        return await context.OrganizationMembers
            .CountAsync(m => m.OrganizationId == organizationId
                          && m.Role == MembershipRole.Owner
                          && m.Status == MembershipStatus.Active, cancellationToken);
    }

    public async Task AddInvitationAsync(Invitation invitation, CancellationToken cancellationToken = default)
    {
        await context.Invitations.AddAsync(invitation, cancellationToken);
    }

    public async Task<Invitation?> GetInvitationByIdAsync(
        Guid invitationId,
        Guid organizationId,
        CancellationToken cancellationToken = default)
    {
        return await context.Invitations
            .FirstOrDefaultAsync(i => i.Id == invitationId && i.OrganizationId == organizationId, cancellationToken);
    }

    public async Task<Invitation?> GetInvitationByTokenHashAsync(
        string tokenHash,
        CancellationToken cancellationToken = default)
    {
        return await context.Invitations
            .FirstOrDefaultAsync(i => i.TokenHash == tokenHash, cancellationToken);
    }

    public async Task<Invitation?> GetPendingInvitationByEmailAsync(
        Guid organizationId,
        string email,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        return await context.Invitations
            .FirstOrDefaultAsync(
                i => i.OrganizationId == organizationId
                  && i.Email == normalizedEmail
                  && i.Status == InvitationStatus.Pending,
                cancellationToken);
    }

    public async Task<IReadOnlyList<Invitation>> GetInvitationsByOrganizationAsync(
        Guid organizationId,
        InvitationStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        var query = context.Invitations
            .AsNoTracking()
            .Where(i => i.OrganizationId == organizationId);

        if (status.HasValue)
        {
            query = query.Where(i => i.Status == status.Value);
        }

        return await query
            .OrderByDescending(i => i.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await context.SaveChangesAsync(cancellationToken);
    }
}
