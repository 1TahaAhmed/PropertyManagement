using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PropertyManagement.Domain.Entities.Leases;
using PropertyManagement.Domain.Entities.Properties;

namespace PropertyManagement.Infrastructure.Persistence.Configurations.Leases;

public sealed class LeaseConfiguration : IEntityTypeConfiguration<Lease>
{
    public void Configure(EntityTypeBuilder<Lease> builder)
    {
        builder.ToTable("Leases");

        builder.HasKey(l => l.Id);

        builder.Property(l => l.OrganizationId)
            .IsRequired();

        builder.Property(l => l.PropertyId)
            .IsRequired();

        builder.Property(l => l.UnitId)
            .IsRequired();

        builder.Property(l => l.StartDate)
            .IsRequired();

        builder.Property(l => l.EndDate)
            .IsRequired();

        builder.Property(l => l.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(l => l.RentAmount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(l => l.SecurityDeposit)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(l => l.Frequency)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(l => l.DueDayOfMonth)
            .IsRequired();

        builder.Property(l => l.GracePeriodDays)
            .IsRequired();

        builder.Property(l => l.LateFeePercentage)
            .HasPrecision(5, 2)
            .IsRequired();

        builder.Property(l => l.CreatedAtUtc)
            .IsRequired();

        builder.Property(l => l.UpdatedAtUtc);

        builder.Property(l => l.RowVersion)
            .IsRowVersion();

        builder.OwnsOne(l => l.TerminationDetails, term =>
        {
            term.Property(t => t.TerminationDate)
                .HasColumnName("TerminationDate");

            term.Property(t => t.Reason)
                .HasColumnName("TerminationReason")
                .HasMaxLength(500);

            term.Property(t => t.Notes)
                .HasColumnName("TerminationNotes")
                .HasMaxLength(2000);

            term.Property(t => t.TerminatedByUserId)
                .HasColumnName("TerminatedByUserId");

            term.Property(t => t.TerminatedAtUtc)
                .HasColumnName("TerminatedAtUtc");
        });

        builder.HasOne<Property>()
            .WithMany()
            .HasForeignKey(l => l.PropertyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Unit>()
            .WithMany()
            .HasForeignKey(l => l.UnitId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Lease>()
            .WithMany()
            .HasForeignKey(l => l.ParentLeaseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(l => l.Residents)
            .WithOne()
            .HasForeignKey(lr => lr.LeaseId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(l => new { l.OrganizationId, l.PropertyId, l.Status });
        builder.HasIndex(l => new { l.OrganizationId, l.UnitId, l.Status });
        builder.HasIndex(l => new { l.OrganizationId, l.UnitId, l.StartDate, l.EndDate });
    }
}
