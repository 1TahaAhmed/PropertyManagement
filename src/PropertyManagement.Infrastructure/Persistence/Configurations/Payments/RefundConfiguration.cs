using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PropertyManagement.Domain.Entities.Payments;

namespace PropertyManagement.Infrastructure.Persistence.Configurations.Payments;

public sealed class RefundConfiguration : IEntityTypeConfiguration<Refund>
{
    public void Configure(EntityTypeBuilder<Refund> builder)
    {
        builder.ToTable("Refunds", t =>
        {
            t.HasCheckConstraint("CK_Refunds_Amount", "[Amount] > 0");
        });

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();
        builder.Property(r => r.OrganizationId).IsRequired();
        builder.Property(r => r.PaymentId).IsRequired();
        builder.Property(r => r.Amount).HasPrecision(18, 2).IsRequired();
        builder.Property(r => r.Currency).HasMaxLength(3).IsRequired();
        builder.Property(r => r.Status).HasConversion<int>().IsRequired();
        builder.Property(r => r.ProviderRefundId).HasMaxLength(200);
        builder.Property(r => r.Reason).HasMaxLength(500).IsRequired();
        builder.Property(r => r.IdempotencyKey).HasMaxLength(150);
        builder.Property(r => r.FailureReason).HasMaxLength(500);
        builder.Property(r => r.RequestedByUserId);
        builder.Property(r => r.CreatedAtUtc).IsRequired();
        builder.Property(r => r.CompletedAtUtc);
        builder.Property(r => r.RowVersion).IsRowVersion();

        builder.HasIndex(r => new { r.OrganizationId, r.IdempotencyKey })
            .IsUnique()
            .HasFilter("[IdempotencyKey] IS NOT NULL");

        builder.HasIndex(r => new { r.PaymentId, r.ProviderRefundId })
            .IsUnique()
            .HasFilter("[ProviderRefundId] IS NOT NULL");

        builder.HasIndex(r => new { r.OrganizationId, r.PaymentId, r.Status });
    }
}
