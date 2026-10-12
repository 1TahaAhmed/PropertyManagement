using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PropertyManagement.Domain.Entities.Authentication;

namespace PropertyManagement.Infrastructure.Persistence.Configurations;

public sealed class EmailConfirmationChallengeConfiguration
    : IEntityTypeConfiguration<EmailConfirmationChallenge>
{
    public void Configure(
        EntityTypeBuilder<EmailConfirmationChallenge> builder)
    {
        builder.ToTable("EmailConfirmationChallenges");

        builder.HasKey(challenge => challenge.Id);

        builder.Property(challenge => challenge.Id)
            .ValueGeneratedNever();

        builder.Property(challenge => challenge.UserId)
            .IsRequired();

        builder.Property(challenge => challenge.ProtectedCode)
            .HasMaxLength(512)
            .IsRequired();

        builder.Property(challenge => challenge.CreatedAt)
            .IsRequired();

        builder.Property(challenge => challenge.ExpiresAt)
            .IsRequired();

        builder.Property(challenge => challenge.FailedAttempts)
            .IsRequired();

        builder.Property(challenge => challenge.UsedAt);

        builder.Property(challenge => challenge.RevokedAt);

        builder.Property(challenge => challenge.RevocationReason)
            .HasMaxLength(100);

        builder.HasIndex(challenge => challenge.UserId);

        builder.HasIndex(challenge => challenge.ExpiresAt);
    }
}
