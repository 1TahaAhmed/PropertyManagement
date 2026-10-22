using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PropertyManagement.Domain.Entities.Properties;

namespace PropertyManagement.Infrastructure.Persistence.Configurations;

public sealed class PropertyConfiguration : IEntityTypeConfiguration<Property>
{
    public void Configure(EntityTypeBuilder<Property> builder)
    {
        builder.ToTable("Properties");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .ValueGeneratedNever();

        builder.Property(p => p.TenantId)
            .IsRequired();

        builder.Property(p => p.Name)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(p => p.Address)
            .HasMaxLength(300)
            .IsRequired();

        builder.Property(p => p.Description)
            .HasMaxLength(1000);

        builder.Property(p => p.Type)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(p => p.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(p => p.CreatedAt)
            .IsRequired();

        builder.HasIndex(p => p.TenantId)
            .IsUnique();

        builder.HasIndex(p => new { p.TenantId, p.Name })
            .IsUnique();

        builder.HasMany(p => p.Units)
            .WithOne()
            .HasForeignKey(u => u.PropertyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(p => p.RowVersion)
            .IsRowVersion();
    }
}