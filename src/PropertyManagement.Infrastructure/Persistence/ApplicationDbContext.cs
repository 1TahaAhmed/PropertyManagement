using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PropertyManagement.Domain.Entities.Authentication;
using PropertyManagement.Domain.Entities.Organizations;
using PropertyManagement.Domain.Entities.Properties;
using PropertyManagement.Infrastructure.Identity;

namespace PropertyManagement.Infrastructure.Persistence
{
    public class ApplicationDbContext :
        IdentityDbContext<
        ApplicationUser,
        IdentityRole<Guid>,
        Guid>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.ApplyConfigurationsFromAssembly(
                typeof(ApplicationDbContext).Assembly);
        }

        public DbSet<EmailConfirmationChallenge>
            EmailConfirmationChallenges => 
            Set<EmailConfirmationChallenge>();

        public DbSet<EmailConfirmationResendAttempt>
            EmailConfirmationResendAttempts =>
            Set<EmailConfirmationResendAttempt>();

        public DbSet<Property> Properties => Set<Property>();
        public DbSet<Unit> Units => Set<Unit>();

        public DbSet<Organization> Organizations => Set<Organization>();
        public DbSet<OrganizationMember> OrganizationMembers => Set<OrganizationMember>();
        public DbSet<Invitation> Invitations => Set<Invitation>();
        public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    }
}