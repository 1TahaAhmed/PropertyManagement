using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PropertyManagement.Domain.Entities.Organizations;
using PropertyManagement.Domain.Entities.Residents;

namespace PropertyManagement.Infrastructure.Persistence.Configurations;

public sealed class ResidentConfiguration : IEntityTypeConfiguration<Resident>
{
    public void Configure(EntityTypeBuilder<Resident> builder)
    {
        builder.ToTable("Residents");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id)
            .ValueGeneratedNever();

        builder.Property(r => r.OrganizationId)
            .IsRequired();

        builder.Property(r => r.FirstName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(r => r.MiddleName)
            .HasMaxLength(100);

        builder.Property(r => r.LastName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(r => r.PreferredName)
            .HasMaxLength(100);

        builder.Property(r => r.Phone)
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(r => r.AlternatePhone)
            .HasMaxLength(30);

        builder.Property(r => r.Email)
            .HasMaxLength(256);

        builder.Property(r => r.NationalIdOrPassport)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(r => r.Notes)
            .HasMaxLength(2000)
            .IsRequired();

        builder.Property(r => r.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(r => r.UserId);

        builder.Property(r => r.CreatedAtUtc)
            .IsRequired();

        builder.Property(r => r.UpdatedAtUtc);

        builder.Property(r => r.RowVersion)
            .IsRowVersion();

        builder.HasMany(r => r.EmergencyContacts)
            .WithOne()
            .HasForeignKey(e => e.ResidentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Organization>()
            .WithMany()
            .HasForeignKey(r => r.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(r => r.OrganizationId);
        builder.HasIndex(r => new { r.OrganizationId, r.Status });
        builder.HasIndex(r => new { r.OrganizationId, r.LastName, r.FirstName });
        builder.HasIndex(r => new { r.OrganizationId, r.Phone });
        builder.HasIndex(r => new { r.OrganizationId, r.Email });
    }
}
