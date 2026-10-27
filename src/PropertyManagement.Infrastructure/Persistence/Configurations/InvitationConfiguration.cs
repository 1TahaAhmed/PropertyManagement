using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PropertyManagement.Domain.Entities.Organizations;

namespace PropertyManagement.Infrastructure.Persistence.Configurations;

public sealed class InvitationConfiguration : IEntityTypeConfiguration<Invitation>
{
    public void Configure(EntityTypeBuilder<Invitation> builder)
    {
        builder.ToTable("Invitations");

        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).ValueGeneratedNever();

        builder.Property(i => i.OrganizationId).IsRequired();

        builder.Property(i => i.Email)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(i => i.Role)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(i => i.TokenHash)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(i => i.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(i => i.ExpiresAtUtc).IsRequired();
        builder.Property(i => i.CreatedAtUtc).IsRequired();

        builder.HasIndex(i => i.TokenHash).IsUnique();
        builder.HasIndex(i => new { i.OrganizationId, i.Email });

        builder.HasOne<Organization>()
            .WithMany()
            .HasForeignKey(i => i.OrganizationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}