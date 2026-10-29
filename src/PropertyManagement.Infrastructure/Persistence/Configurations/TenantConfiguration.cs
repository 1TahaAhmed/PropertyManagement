using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PropertyManagement.Domain.Entities.Organizations;
using PropertyManagement.Domain.Entities.Tenants;

namespace PropertyManagement.Infrastructure.Persistence.Configurations;

public sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("Tenants");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id)
            .ValueGeneratedNever();

        builder.Property(t => t.OrganizationId)
            .IsRequired();

        builder.Property(t => t.FirstName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(t => t.LastName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(t => t.Email)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(t => t.PhoneNumber)
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(t => t.NationalIdOrPassport)
            .HasMaxLength(50);

        builder.Property(t => t.EmergencyContactName)
            .HasMaxLength(150);

        builder.Property(t => t.EmergencyContactPhone)
            .HasMaxLength(30);

        builder.Property(t => t.Notes)
            .HasMaxLength(2000);

        builder.Property(t => t.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(t => t.CreatedAt)
            .IsRequired();

        builder.Property(t => t.RowVersion)
            .IsRowVersion();

        builder.HasOne<Organization>()
            .WithMany()
            .HasForeignKey(t => t.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(t => t.OrganizationId);
        builder.HasIndex(t => new { t.OrganizationId, t.Email });
        builder.HasIndex(t => new { t.OrganizationId, t.PhoneNumber });
    }
}
