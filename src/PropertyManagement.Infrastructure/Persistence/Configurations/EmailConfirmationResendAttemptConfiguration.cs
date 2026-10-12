using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PropertyManagement.Domain.Entities.Authentication;
using System;
using System.Collections.Generic;
using System.Text;

namespace PropertyManagement.Infrastructure.Persistence.Configurations
{
    public sealed class EmailConfirmationResendAttemptConfiguration
        : IEntityTypeConfiguration<EmailConfirmationResendAttempt>
    {
        public void Configure(EntityTypeBuilder<EmailConfirmationResendAttempt> builder)
        {
            builder.ToTable("EmailConfirmationResendAttempts");

            builder.HasKey(attempt => attempt.Id);

            builder.Property(attempt => attempt.Id)
                .ValueGeneratedNever();

            builder.Property(attempt => attempt.UserId)
                .IsRequired();

            builder.Property(attempt => attempt.IpAddress)
                .HasMaxLength(64)
                .IsRequired();

            builder.Property(attempt => attempt.RequestedAt)
                .IsRequired();

            builder.HasIndex(attempt => new
            {
                attempt.UserId,
                attempt.RequestedAt
            });

            builder.HasIndex(attempt => new
            {
                attempt.IpAddress,
                attempt.RequestedAt
            });
        }
    }
}
