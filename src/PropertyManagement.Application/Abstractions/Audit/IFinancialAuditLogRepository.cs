using PropertyManagement.Domain.Entities.Audit;

namespace PropertyManagement.Application.Abstractions.Audit;

public sealed record FinancialAuditLogFilter(
    Guid OrganizationId,
    string? EntityType = null,
    Guid? EntityId = null,
    string? Action = null,
    int PageNumber = 1,
    int PageSize = 20);

public interface IFinancialAuditLogRepository
{
    Task AddAsync(FinancialAuditLog entry, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<FinancialAuditLog> Items, int TotalCount)> GetPagedAsync(
        FinancialAuditLogFilter filter,
        CancellationToken cancellationToken = default);
}
