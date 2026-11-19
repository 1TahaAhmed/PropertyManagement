using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PropertyManagement.Domain.Entities.Audit;
using PropertyManagement.Domain.Entities.Organizations;

namespace PropertyManagement.Infrastructure.Persistence.Configurations.Audit;

public sealed class FinancialAuditLogConfiguration : IEntityTypeConfiguration<FinancialAuditLog>
{
    public void Configure(EntityTypeBuilder<FinancialAuditLog> builder)
    {
        builder.ToTable("FinancialAuditLogs");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();
        builder.Property(a => a.OrganizationId).IsRequired();
        builder.Property(a => a.ActorUserId);
        builder.Property(a => a.Action).HasMaxLength(150).IsRequired();
        builder.Property(a => a.EntityType).HasMaxLength(100).IsRequired();
        builder.Property(a => a.EntityId).IsRequired();
        builder.Property(a => a.DetailsJson).HasMaxLength(4000).IsRequired();
        builder.Property(a => a.CorrelationId).HasMaxLength(150);
        builder.Property(a => a.OccurredAtUtc).IsRequired();

        builder.HasOne<Organization>()
            .WithMany()
            .HasForeignKey(a => a.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(a => new { a.OrganizationId, a.EntityType, a.EntityId });
        builder.HasIndex(a => new { a.OrganizationId, a.OccurredAtUtc });
    }
}
