using PropertyManagement.Domain.Entities.Billing;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Application.Abstractions.Billing;

public sealed record InvoiceFilter(
    Guid OrganizationId,
    Guid? LeaseId = null,
    Guid? ResidentId = null,
    Guid? PropertyId = null,
    Guid? UnitId = null,
    InvoiceStatus? Status = null,
    bool OnlyOverdue = false,
    DateOnly? DueBefore = null,
    DateOnly? PeriodStart = null,
    DateOnly? PeriodEnd = null,
    string? InvoiceNumber = null,
    int PageNumber = 1,
    int PageSize = 10,
    bool IncludeDetails = false,
    bool AsNoTracking = false);

public interface IInvoiceRepository
{
    Task<Invoice?> GetByIdAsync(Guid id, Guid organizationId, CancellationToken cancellationToken = default);
    Task<Invoice?> GetByInvoiceNumberAsync(string invoiceNumber, Guid organizationId, CancellationToken cancellationToken = default);
    Task<Invoice?> GetByIdempotencyKeyAsync(string idempotencyKey, Guid organizationId, CancellationToken cancellationToken = default);
    Task<bool> ExistsActiveByIdempotencyKeyAsync(string idempotencyKey, Guid organizationId, CancellationToken cancellationToken = default);
    Task<bool> ExistsActiveRentOverlapAsync(Guid leaseId, Guid organizationId, DateOnly periodStart, DateOnly periodEnd, CancellationToken cancellationToken = default);
    Task AcquireInvoiceLockAsync(Guid invoiceId, Guid organizationId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Invoice>> GetByLeaseIdAsync(Guid leaseId, Guid organizationId, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<Invoice> Items, int TotalCount)> GetPagedAsync(InvoiceFilter filter, CancellationToken cancellationToken = default);
    Task AddAsync(Invoice invoice, CancellationToken cancellationToken = default);
    void SetRowVersion(Invoice invoice, byte[] rowVersion);
    void Update(Invoice invoice);
}
