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

    public async Task<IReadOnlyList<OrganizationMember>> GetMembersAsync(Guid organizationId, CancellationToken cancellationToken = default)
    {
        return await context.OrganizationMembers
            .Where(m => m.OrganizationId == organizationId)
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

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> CountActiveOwnersAsync(Guid organizationId, CancellationToken cancellationToken = default)
    {
        return await context.OrganizationMembers
            .CountAsync(m => m.OrganizationId == organizationId
                          && m.Role == MembershipRole.Owner
                          && m.Status == MembershipStatus.Active, cancellationToken);
    }
}