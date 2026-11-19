using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PropertyManagement.Domain.Entities.Billing;
using PropertyManagement.Domain.Entities.Leases;
using PropertyManagement.Domain.Entities.Payments;
using PropertyManagement.Domain.Entities.Residents;

namespace PropertyManagement.Infrastructure.Persistence.Configurations.Payments;

public sealed class PaymentIntentConfiguration : IEntityTypeConfiguration<PaymentIntent>
{
    public void Configure(EntityTypeBuilder<PaymentIntent> builder)
    {
        builder.ToTable("PaymentIntents", t =>
        {
            t.HasCheckConstraint("CK_PaymentIntents_Amount", "[Amount] > 0");
        });

        builder.HasKey(pi => pi.Id);
        builder.Property(pi => pi.Id).ValueGeneratedNever();
        builder.Property(pi => pi.OrganizationId).IsRequired();
        builder.Property(pi => pi.ResidentId).IsRequired();
        builder.Property(pi => pi.LeaseId);
        builder.Property(pi => pi.TargetInvoiceId);
        builder.Property(pi => pi.Amount).HasPrecision(18, 2).IsRequired();
        builder.Property(pi => pi.Currency).HasMaxLength(3).IsRequired();
        builder.Property(pi => pi.PaymentMethod).HasConversion<int>().IsRequired();
        builder.Property(pi => pi.Provider).HasConversion<int>().IsRequired();
        builder.Property(pi => pi.Status).HasConversion<int>().IsRequired();
        builder.Property(pi => pi.ProviderIntentId).HasMaxLength(200);
        builder.Property(pi => pi.ClientSecret).HasMaxLength(500);
        builder.Property(pi => pi.ActionUrl).HasMaxLength(1000);
        builder.Property(pi => pi.IdempotencyKey).HasMaxLength(150);
        builder.Property(pi => pi.FailureReason).HasMaxLength(500);
        builder.Property(pi => pi.ExpiresAtUtc).IsRequired();
        builder.Property(pi => pi.CreatedAtUtc).IsRequired();
        builder.Property(pi => pi.UpdatedAtUtc);
        builder.Property(pi => pi.RowVersion).IsRowVersion();

        builder.Ignore(pi => pi.DomainEvents);

        builder.HasOne<Resident>()
            .WithMany()
            .HasForeignKey(pi => pi.ResidentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Lease>()
            .WithMany()
            .HasForeignKey(pi => pi.LeaseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Invoice>()
            .WithMany()
            .HasForeignKey(pi => pi.TargetInvoiceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(pi => new { pi.OrganizationId, pi.IdempotencyKey })
            .IsUnique()
            .HasFilter("[IdempotencyKey] IS NOT NULL");

        builder.HasIndex(pi => new { pi.Provider, pi.ProviderIntentId })
            .IsUnique()
            .HasFilter("[ProviderIntentId] IS NOT NULL");

        builder.HasIndex(pi => new { pi.OrganizationId, pi.ResidentId, pi.Status });
    }
}
