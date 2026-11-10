using PropertyManagement.Domain.Entities.Leases;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using PropertyManagement.Application.Abstractions;
using PropertyManagement.Application.Abstractions.Security;
using PropertyManagement.Domain.Common;
using PropertyManagement.Domain.Entities.Authentication;
using PropertyManagement.Domain.Entities.Organizations;
using PropertyManagement.Domain.Entities.Properties;
using PropertyManagement.Domain.Entities.Residents;
using PropertyManagement.Domain.Entities.Tenants;
using PropertyManagement.Infrastructure.Identity;

namespace PropertyManagement.Infrastructure.Persistence
{
    public class ApplicationDbContext :
        IdentityDbContext<
        ApplicationUser,
        IdentityRole<Guid>,
        Guid>,
        IUnitOfWork
    {
        private readonly ICurrentUser? _currentUser;

        public ApplicationDbContext(
            DbContextOptions<ApplicationDbContext> options,
            ICurrentUser? currentUser = null)
            : base(options)
        {
            _currentUser = currentUser;
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

            builder.Entity<Tenant>().HasQueryFilter(t =>
                t.OrganizationId == (_currentUser != null && _currentUser.IsAuthenticated && _currentUser.OrganizationId.HasValue
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
        }

        public DbSet<EmailConfirmationChallenge>
            EmailConfirmationChallenges => 
            Set<EmailConfirmationChallenge>();

        public DbSet<EmailConfirmationResendAttempt>
            EmailConfirmationResendAttempts =>
            Set<EmailConfirmationResendAttempt>();

        public DbSet<Property> Properties => Set<Property>();
        public DbSet<Unit> Units => Set<Unit>();
        public DbSet<Tenant> Tenants => Set<Tenant>();
        public DbSet<Resident> Residents => Set<Resident>();
        public DbSet<EmergencyContact> EmergencyContacts => Set<EmergencyContact>();
        public DbSet<Organization> Organizations => Set<Organization>();
        public DbSet<OrganizationMember> OrganizationMembers => Set<OrganizationMember>();
        public DbSet<Invitation> Invitations => Set<Invitation>();
        public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
        public DbSet<Lease> Leases => Set<Lease>();
        public DbSet<LeaseResident> LeaseResidents => Set<LeaseResident>();

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            if (_currentUser != null)
            {
                if (_currentUser.IsAuthenticated && _currentUser.OrganizationId.HasValue)
                {
                    var currentOrgId = _currentUser.OrganizationId.Value;

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
                            if (entry.Entity.OrganizationId != currentOrgId)
                            {
                                throw new InvalidOperationException("Cross-tenant modification detected.");
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

            return await base.SaveChangesAsync(cancellationToken);
        }

        public async Task<IDbTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
        {
            var transaction = await Database.BeginTransactionAsync(cancellationToken);
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
}
