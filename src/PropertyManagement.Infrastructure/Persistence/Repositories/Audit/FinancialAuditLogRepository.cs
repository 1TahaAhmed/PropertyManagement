using Microsoft.EntityFrameworkCore;
using PropertyManagement.Application.Abstractions.Audit;
using PropertyManagement.Domain.Entities.Audit;

namespace PropertyManagement.Infrastructure.Persistence.Repositories.Audit;

public sealed class FinancialAuditLogRepository(ApplicationDbContext context) : IFinancialAuditLogRepository
{
    public async Task AddAsync(FinancialAuditLog entry, CancellationToken cancellationToken = default)
    {
        await context.FinancialAuditLogs.AddAsync(entry, cancellationToken);
    }

    public async Task<(IReadOnlyList<FinancialAuditLog> Items, int TotalCount)> GetPagedAsync(
        FinancialAuditLogFilter filter,
        CancellationToken cancellationToken = default)
    {
        var query = context.FinancialAuditLogs
            .AsNoTracking()
            .Where(a => a.OrganizationId == filter.OrganizationId);

        if (!string.IsNullOrWhiteSpace(filter.EntityType))
        {
            var entityType = filter.EntityType.Trim();
            query = query.Where(a => a.EntityType == entityType);
        }

        if (filter.EntityId.HasValue)
        {
            query = query.Where(a => a.EntityId == filter.EntityId.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.Action))
        {
            var action = filter.Action.Trim();
            query = query.Where(a => a.Action == action);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(a => a.OccurredAtUtc)
            .Skip((filter.PageNumber - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }
}
