using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PropertyManagement.Domain.Entities.Authentication;

namespace PropertyManagement.Infrastructure.Persistence.Configurations;

public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.Property(r => r.UserId).IsRequired();
        builder.Property(r => r.TokenHash)
            .HasMaxLength(128)
            .IsRequired();
        builder.Property(r => r.ExpiresAtUtc).IsRequired();
        builder.Property(r => r.CreatedAtUtc).IsRequired();
        builder.Property(r => r.ReplacedByTokenHash)
            .HasMaxLength(128);
        builder.Property(r => r.OrganizationId)
            .IsRequired(false);
        builder.Property(r => r.RowVersion)
            .IsRowVersion();

        builder.HasIndex(r => r.TokenHash).IsUnique();
        builder.HasIndex(r => r.UserId);
        builder.HasIndex(r => r.OrganizationId);
    }
}
