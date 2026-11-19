using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using PropertyManagement.Application.Abstractions;
using PropertyManagement.Application.Abstractions.Events;
using PropertyManagement.Application.Abstractions.Security;
using PropertyManagement.Domain.Common;
using PropertyManagement.Domain.Entities.Authentication;
using PropertyManagement.Domain.Entities.Audit;
using PropertyManagement.Domain.Entities.Billing;
using PropertyManagement.Domain.Entities.Ledger;
using PropertyManagement.Domain.Entities.Payments;
using PropertyManagement.Domain.Entities.Leases;
using PropertyManagement.Domain.Enums;
using PropertyManagement.Infrastructure.Persistence.Entities;
using PropertyManagement.Domain.Entities.Organizations;
using PropertyManagement.Domain.Entities.Properties;
using PropertyManagement.Domain.Entities.Residents;
using PropertyManagement.Infrastructure.Identity;

namespace PropertyManagement.Infrastructure.Persistence;

public class ApplicationDbContext :
    IdentityDbContext<
    ApplicationUser,
    IdentityRole<Guid>,
    Guid>,
    IUnitOfWork
{
    private readonly ICurrentUser? _currentUser;
    private readonly IDomainEventDispatcher? _domainEventDispatcher;
    private readonly ISystemTenantContext? _systemTenantContext;

    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options,
        ICurrentUser? currentUser = null,
        IDomainEventDispatcher? domainEventDispatcher = null,
        ISystemTenantContext? systemTenantContext = null)
        : base(options)
    {
        _currentUser = currentUser;
        _domainEventDispatcher = domainEventDispatcher;
        _systemTenantContext = systemTenantContext;
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ApplyConfigurationsFromAssembly(
            typeof(ApplicationDbContext).Assembly);

        builder.Entity<Property>().HasQueryFilter(p =>
            p.OrganizationId == (_currentUser != null && _currentUser.IsAuthenticated && _currentUser.OrganizationId.HasValue
                ? _currentUser.OrganizationId.Value
                : Guid.Empty));

        builder.Entity<Unit>().HasQueryFilter(u =>
            u.OrganizationId == (_currentUser != null && _currentUser.IsAuthenticated && _currentUser.OrganizationId.HasValue
                ? _currentUser.OrganizationId.Value
                : Guid.Empty));

        builder.Entity<Resident>().HasQueryFilter(r =>
            r.OrganizationId == (_currentUser != null && _currentUser.IsAuthenticated && _currentUser.OrganizationId.HasValue
                ? _currentUser.OrganizationId.Value
                : Guid.Empty));

        builder.Entity<EmergencyContact>().HasQueryFilter(e =>
            e.OrganizationId == (_currentUser != null && _currentUser.IsAuthenticated && _currentUser.OrganizationId.HasValue
                ? _currentUser.OrganizationId.Value
                : Guid.Empty));

        builder.Entity<Lease>().HasQueryFilter(l =>
            l.OrganizationId == (_currentUser != null && _currentUser.IsAuthenticated && _currentUser.OrganizationId.HasValue
                ? _currentUser.OrganizationId.Value
                : Guid.Empty));

        builder.Entity<LeaseResident>().HasQueryFilter(lr =>
            lr.OrganizationId == (_currentUser != null && _currentUser.IsAuthenticated && _currentUser.OrganizationId.HasValue
                ? _currentUser.OrganizationId.Value
                : Guid.Empty));

        builder.Entity<Invoice>().HasQueryFilter(i =>
            i.OrganizationId == (_currentUser != null && _currentUser.IsAuthenticated && _currentUser.OrganizationId.HasValue
                ? _currentUser.OrganizationId.Value
                : (_systemTenantContext != null && _systemTenantContext.ScopedOrganizationId.HasValue
                    ? _systemTenantContext.ScopedOrganizationId.Value
                    : Guid.Empty)));

        builder.Entity<InvoiceLine>().HasQueryFilter(l =>
            l.OrganizationId == (_currentUser != null && _currentUser.IsAuthenticated && _currentUser.OrganizationId.HasValue
                ? _currentUser.OrganizationId.Value
                : Guid.Empty));

        builder.Entity<InvoiceAdjustment>().HasQueryFilter(a =>
            a.OrganizationId == (_currentUser != null && _currentUser.IsAuthenticated && _currentUser.OrganizationId.HasValue
                ? _currentUser.OrganizationId.Value
                : Guid.Empty));

        builder.Entity<Charge>().HasQueryFilter(c =>
            c.OrganizationId == (_currentUser != null && _currentUser.IsAuthenticated && _currentUser.OrganizationId.HasValue
                ? _currentUser.OrganizationId.Value
                : Guid.Empty));

        builder.Entity<PaymentIntent>().HasQueryFilter(pi =>
            pi.OrganizationId == (_currentUser != null && _currentUser.IsAuthenticated && _currentUser.OrganizationId.HasValue
                ? _currentUser.OrganizationId.Value
                : (_systemTenantContext != null && _systemTenantContext.ScopedOrganizationId.HasValue
                    ? _systemTenantContext.ScopedOrganizationId.Value
                    : Guid.Empty)));

        builder.Entity<Payment>().HasQueryFilter(p =>
            p.OrganizationId == (_currentUser != null && _currentUser.IsAuthenticated && _currentUser.OrganizationId.HasValue
                ? _currentUser.OrganizationId.Value
                : (_systemTenantContext != null && _systemTenantContext.ScopedOrganizationId.HasValue
                    ? _systemTenantContext.ScopedOrganizationId.Value
                    : Guid.Empty)));

        builder.Entity<PaymentAllocation>().HasQueryFilter(a =>
            a.OrganizationId == (_currentUser != null && _currentUser.IsAuthenticated && _currentUser.OrganizationId.HasValue
                ? _currentUser.OrganizationId.Value
                : (_systemTenantContext != null && _systemTenantContext.ScopedOrganizationId.HasValue
                    ? _systemTenantContext.ScopedOrganizationId.Value
                    : Guid.Empty)));

        builder.Entity<Refund>().HasQueryFilter(r =>
            r.OrganizationId == (_currentUser != null && _currentUser.IsAuthenticated && _currentUser.OrganizationId.HasValue
                ? _currentUser.OrganizationId.Value
                : (_systemTenantContext != null && _systemTenantContext.ScopedOrganizationId.HasValue
                    ? _systemTenantContext.ScopedOrganizationId.Value
                    : Guid.Empty)));

        builder.Entity<PaymentReconciliationRecord>().HasQueryFilter(r =>
            r.OrganizationId == (_currentUser != null && _currentUser.IsAuthenticated && _currentUser.OrganizationId.HasValue
                ? _currentUser.OrganizationId.Value
                : (_systemTenantContext != null && _systemTenantContext.ScopedOrganizationId.HasValue
                    ? _systemTenantContext.ScopedOrganizationId.Value
                    : Guid.Empty)));

        builder.Entity<JournalEntry>().HasQueryFilter(j =>
            j.OrganizationId == (_currentUser != null && _currentUser.IsAuthenticated && _currentUser.OrganizationId.HasValue
                ? _currentUser.OrganizationId.Value
                : (_systemTenantContext != null && _systemTenantContext.ScopedOrganizationId.HasValue
                    ? _systemTenantContext.ScopedOrganizationId.Value
                    : Guid.Empty)));

        builder.Entity<JournalLine>().HasQueryFilter(l =>
            l.OrganizationId == (_currentUser != null && _currentUser.IsAuthenticated && _currentUser.OrganizationId.HasValue
                ? _currentUser.OrganizationId.Value
                : (_systemTenantContext != null && _systemTenantContext.ScopedOrganizationId.HasValue
                    ? _systemTenantContext.ScopedOrganizationId.Value
                    : Guid.Empty)));

        builder.Entity<FinancialAuditLog>().HasQueryFilter(a =>
            a.OrganizationId == (_currentUser != null && _currentUser.IsAuthenticated && _currentUser.OrganizationId.HasValue
                ? _currentUser.OrganizationId.Value
                : (_systemTenantContext != null && _systemTenantContext.ScopedOrganizationId.HasValue
                    ? _systemTenantContext.ScopedOrganizationId.Value
                    : Guid.Empty)));
    }

    public DbSet<EmailConfirmationChallenge>
        EmailConfirmationChallenges =>
        Set<EmailConfirmationChallenge>();

    public DbSet<EmailConfirmationResendAttempt>
        EmailConfirmationResendAttempts =>
        Set<EmailConfirmationResendAttempt>();

    public DbSet<Property> Properties => Set<Property>();
    public DbSet<Unit> Units => Set<Unit>();
    public DbSet<Resident> Residents => Set<Resident>();
    public DbSet<EmergencyContact> EmergencyContacts => Set<EmergencyContact>();
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<OrganizationMember> OrganizationMembers => Set<OrganizationMember>();
    public DbSet<Invitation> Invitations => Set<Invitation>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Lease> Leases => Set<Lease>();
    public DbSet<LeaseResident> LeaseResidents => Set<LeaseResident>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceLine> InvoiceLines => Set<InvoiceLine>();
    public DbSet<InvoiceAdjustment> InvoiceAdjustments => Set<InvoiceAdjustment>();
    public DbSet<Charge> Charges => Set<Charge>();
    public DbSet<InvoiceNumberSequence> InvoiceNumberSequences => Set<InvoiceNumberSequence>();
    public DbSet<PaymentIntent> PaymentIntents => Set<PaymentIntent>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<PaymentAllocation> PaymentAllocations => Set<PaymentAllocation>();
    public DbSet<Refund> Refunds => Set<Refund>();
    public DbSet<ProcessedWebhookEvent> ProcessedWebhookEvents => Set<ProcessedWebhookEvent>();
    public DbSet<PaymentReconciliationRecord> PaymentReconciliationRecords => Set<PaymentReconciliationRecord>();
    public DbSet<PaymentNumberSequence> PaymentNumberSequences => Set<PaymentNumberSequence>();
    public DbSet<JournalEntry> JournalEntries => Set<JournalEntry>();
    public DbSet<JournalLine> JournalLines => Set<JournalLine>();
    public DbSet<JournalEntryNumberSequence> JournalEntryNumberSequences => Set<JournalEntryNumberSequence>();
    public DbSet<FinancialAuditLog> FinancialAuditLogs => Set<FinancialAuditLog>();

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var systemOrgId = _systemTenantContext?.ScopedOrganizationId;
        if (_currentUser != null || systemOrgId.HasValue)
        {
            Guid? resolvedOrgId = _currentUser is { IsAuthenticated: true }
                ? _currentUser.OrganizationId
                : systemOrgId;

            if (resolvedOrgId.HasValue)
            {
                var currentOrgId = resolvedOrgId.Value;

                foreach (var entry in ChangeTracker.Entries<IOrganizationScoped>())
                {
                    if (entry.State == EntityState.Added)
                    {
                        if (entry.Entity.OrganizationId == Guid.Empty)
                        {
                            entry.Property(nameof(IOrganizationScoped.OrganizationId)).CurrentValue = currentOrgId;
                        }
                        else if (entry.Entity.OrganizationId != currentOrgId)
                        {
                            throw new InvalidOperationException("Cross-tenant write operation detected.");
                        }
                    }
                    else if (entry.State == EntityState.Modified)
                    {
                        var originalOrgId = entry.Property(nameof(IOrganizationScoped.OrganizationId)).OriginalValue;
                        if (entry.Entity.OrganizationId != currentOrgId ||
                            (originalOrgId is Guid origGuid && origGuid != currentOrgId))
                        {
                            throw new InvalidOperationException("Cross-tenant modification detected.");
                        }
                    }
                    else if (entry.State == EntityState.Deleted)
                    {
                        var originalOrgId = entry.Property(nameof(IOrganizationScoped.OrganizationId)).OriginalValue;
                        if (entry.Entity.OrganizationId != currentOrgId ||
                            (originalOrgId is Guid origGuid && origGuid != currentOrgId))
                        {
                            throw new InvalidOperationException("Cross-tenant deletion detected.");
                        }
                    }
                }
            }
            else
            {
                var unauthorizedEntries = ChangeTracker.Entries<IOrganizationScoped>()
                    .Any(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted);

                if (unauthorizedEntries)
                {
                    throw new InvalidOperationException("An active tenant context is required for organization-scoped write operations.");
                }
            }
        }

        foreach (var adjustmentEntry in ChangeTracker.Entries<InvoiceAdjustment>())
        {
            if (adjustmentEntry.State is EntityState.Modified or EntityState.Deleted)
            {
                throw new InvalidOperationException("Invoice adjustments are immutable and cannot be modified or deleted.");
            }
        }

        foreach (var lineEntry in ChangeTracker.Entries<InvoiceLine>())
        {
            if (lineEntry.State is EntityState.Modified or EntityState.Deleted)
            {
                var trackedParent = ChangeTracker.Entries<Invoice>()
                    .FirstOrDefault(e => e.Entity.Id == lineEntry.Entity.InvoiceId);

                InvoiceStatus? originalParentStatus = trackedParent is not null
                    ? (InvoiceStatus)trackedParent.Property(nameof(Invoice.Status)).OriginalValue!
                    : await Invoices
                        .IgnoreQueryFilters()
                        .AsNoTracking()
                        .Where(i => i.Id == lineEntry.Entity.InvoiceId)
                        .Select(i => (InvoiceStatus?)i.Status)
                        .FirstOrDefaultAsync(cancellationToken);

                if (originalParentStatus.HasValue && originalParentStatus.Value != InvoiceStatus.Draft)
                {
                    throw new InvalidOperationException("Invoice lines cannot be modified or deleted after the invoice has been issued.");
                }
            }
        }

        foreach (var paymentEntry in ChangeTracker.Entries<Payment>())
        {
            if (paymentEntry.State == EntityState.Deleted)
            {
                throw new InvalidOperationException("Payments cannot be deleted from financial history.");
            }
        }

        foreach (var allocationEntry in ChangeTracker.Entries<PaymentAllocation>())
        {
            if (allocationEntry.State == EntityState.Deleted)
            {
                throw new InvalidOperationException("Payment allocations are immutable financial records; reverse them instead of deleting.");
            }
        }

        foreach (var refundEntry in ChangeTracker.Entries<Refund>())
        {
            if (refundEntry.State == EntityState.Deleted)
            {
                throw new InvalidOperationException("Refunds cannot be deleted from financial history.");
            }
        }

        foreach (var auditEntry in ChangeTracker.Entries<FinancialAuditLog>())
        {
            if (auditEntry.State is EntityState.Modified or EntityState.Deleted)
            {
                throw new InvalidOperationException("Financial audit logs are immutable and cannot be modified or deleted.");
            }
        }

        foreach (var jeEntry in ChangeTracker.Entries<JournalEntry>())
        {
            if (jeEntry.State is EntityState.Modified or EntityState.Deleted)
            {
                var originalStatus = (LedgerEntryStatus)jeEntry.Property(nameof(JournalEntry.Status)).OriginalValue!;
                if (originalStatus == LedgerEntryStatus.Posted)
                {
                    throw new InvalidOperationException("Posted journal entries are immutable and cannot be modified or deleted.");
                }
            }
        }

        foreach (var jlEntry in ChangeTracker.Entries<JournalLine>())
        {
            if (jlEntry.State is EntityState.Modified or EntityState.Deleted)
            {
                var trackedParent = ChangeTracker.Entries<JournalEntry>()
                    .FirstOrDefault(e => e.Entity.Id == jlEntry.Entity.JournalEntryId);

                LedgerEntryStatus? originalParentStatus = trackedParent is not null
                    ? (LedgerEntryStatus)trackedParent.Property(nameof(JournalEntry.Status)).OriginalValue!
                    : await JournalEntries
                        .IgnoreQueryFilters()
                        .AsNoTracking()
                        .Where(j => j.Id == jlEntry.Entity.JournalEntryId)
                        .Select(j => (LedgerEntryStatus?)j.Status)
                        .FirstOrDefaultAsync(cancellationToken);

                if (originalParentStatus == LedgerEntryStatus.Posted)
                {
                    throw new InvalidOperationException("Journal lines belonging to a posted entry are immutable and cannot be modified or deleted.");
                }
            }
        }

        var entitiesWithEvents = ChangeTracker.Entries<IHasDomainEvents>()
            .Select(e => e.Entity)
            .Where(e => e.DomainEvents.Count > 0)
            .ToList();

        var domainEvents = entitiesWithEvents
            .SelectMany(e => e.DomainEvents)
            .ToList();

        var result = await base.SaveChangesAsync(cancellationToken);

        foreach (var entity in entitiesWithEvents)
        {
            entity.ClearDomainEvents();
        }

        if (_domainEventDispatcher is not null && domainEvents.Count > 0)
        {
            await _domainEventDispatcher.DispatchAsync(domainEvents, cancellationToken);
        }

        return result;
    }

    public async Task<IDbTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        var transaction = await Database.BeginTransactionAsync(cancellationToken);
        return new EfDbTransaction(transaction);
    }

    public async Task<IDbTransaction> BeginTransactionAsync(
        System.Data.IsolationLevel isolationLevel,
        CancellationToken cancellationToken = default)
    {
        var transaction = await Database.BeginTransactionAsync(isolationLevel, cancellationToken);
        return new EfDbTransaction(transaction);
    }
}

internal sealed class EfDbTransaction(IDbContextTransaction transaction) : IDbTransaction
{
    public Task CommitAsync(CancellationToken cancellationToken = default)
        => transaction.CommitAsync(cancellationToken);

    public Task RollbackAsync(CancellationToken cancellationToken = default)
        => transaction.RollbackAsync(cancellationToken);

    public ValueTask DisposeAsync()
        => transaction.DisposeAsync();

    public void Dispose()
        => transaction.Dispose();
}
