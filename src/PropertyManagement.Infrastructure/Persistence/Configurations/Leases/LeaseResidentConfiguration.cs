using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PropertyManagement.Domain.Entities.Leases;
using PropertyManagement.Domain.Entities.Residents;

namespace PropertyManagement.Infrastructure.Persistence.Configurations.Leases;

public sealed class LeaseResidentConfiguration : IEntityTypeConfiguration<LeaseResident>
{
    public void Configure(EntityTypeBuilder<LeaseResident> builder)
    {
        builder.ToTable("LeaseResidents");

        builder.HasKey(lr => lr.Id);

        builder.Property(lr => lr.OrganizationId)
            .IsRequired();

        builder.Property(lr => lr.LeaseId)
            .IsRequired();

        builder.Property(lr => lr.ResidentId)
            .IsRequired();

        builder.Property(lr => lr.Role)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(lr => lr.AddedAtUtc)
            .IsRequired();

        builder.HasOne<Resident>()
            .WithMany()
            .HasForeignKey(lr => lr.ResidentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(lr => new { lr.LeaseId, lr.ResidentId })
            .IsUnique();

        builder.HasIndex(lr => new { lr.OrganizationId, lr.ResidentId });
    }
}
