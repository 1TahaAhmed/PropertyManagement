using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PropertyManagement.Domain.Entities.Properties;

namespace PropertyManagement.Infrastructure.Persistence.Configurations;

public sealed class UnitConfiguration : IEntityTypeConfiguration<Unit>
{
    public void Configure(EntityTypeBuilder<Unit> builder)
    {
        builder.ToTable("Units");

        builder.HasKey(u => u.Id);

        builder.Property(u => u.Id)
            .ValueGeneratedNever();

        builder.Property(u => u.TenantId)
            .IsRequired();

        builder.Property(u => u.PropertyId)
            .IsRequired();

        builder.Property(u => u.UnitNumber)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(u => u.FloorNumber);

        builder.Property(u => u.Type)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(u => u.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(u => u.MarketRent)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(u => u.SquareMeters)
            .HasPrecision(10, 2)
            .IsRequired();

        builder.Property(u => u.Bedrooms)
            .IsRequired();

        builder.Property(u => u.Bathrooms)
            .IsRequired();

        builder.Property(u => u.Description)
            .HasMaxLength(1000);

        builder.Property(u => u.CreatedAt)
            .IsRequired();

        builder.HasIndex(u => u.TenantId).IsUnique();
        builder.HasIndex(u => u.PropertyId).IsUnique();

        builder.HasIndex(u => new { u.TenantId, u.PropertyId, u.UnitNumber })
            .IsUnique();

        builder.Property(u => u.RowVersion)
            .IsRowVersion();
    }
}