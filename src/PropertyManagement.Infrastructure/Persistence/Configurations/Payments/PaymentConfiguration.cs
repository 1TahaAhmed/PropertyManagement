using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PropertyManagement.Domain.Entities.Leases;
using PropertyManagement.Domain.Entities.Payments;
using PropertyManagement.Domain.Entities.Residents;

namespace PropertyManagement.Infrastructure.Persistence.Configurations.Payments;

public sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("Payments", t =>
        {
            t.HasCheckConstraint("CK_Payments_Amount", "[Amount] > 0");
            t.HasCheckConstraint("CK_Payments_Balances",
                "CAST([AllocatedAmount] AS DECIMAL(18,2)) >= 0 AND CAST([RefundedAmount] AS DECIMAL(18,2)) >= 0 AND CAST([UnappliedAmount] AS DECIMAL(18,2)) >= 0 AND CAST([ProviderFee] AS DECIMAL(18,2)) >= 0 AND CAST([NetSettlementAmount] AS DECIMAL(18,2)) >= 0 AND (CAST([AllocatedAmount] AS DECIMAL(18,2)) + CAST([RefundedAmount] AS DECIMAL(18,2))) <= CAST([Amount] AS DECIMAL(18,2))");
        });

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();
        builder.Property(p => p.OrganizationId).IsRequired();
        builder.Property(p => p.ResidentId).IsRequired();
        builder.Property(p => p.LeaseId);
        builder.Property(p => p.PaymentIntentId);
        builder.Property(p => p.PaymentNumber).HasMaxLength(50);
        builder.Property(p => p.Amount).HasPrecision(18, 2).IsRequired();
        builder.Property(p => p.Currency).HasMaxLength(3).IsRequired();
        builder.Property(p => p.ProviderFee).HasPrecision(18, 2).IsRequired();
        builder.Property(p => p.NetSettlementAmount).HasPrecision(18, 2).IsRequired();
        builder.Property(p => p.AllocatedAmount).HasPrecision(18, 2).IsRequired();
        builder.Property(p => p.UnappliedAmount).HasPrecision(18, 2).IsRequired();
        builder.Property(p => p.RefundedAmount).HasPrecision(18, 2).IsRequired();
        builder.Property(p => p.PaymentMethod).HasConversion<int>().IsRequired();
        builder.Property(p => p.Provider).HasConversion<int>().IsRequired();
        builder.Property(p => p.Status).HasConversion<int>().IsRequired();
        builder.Property(p => p.OverpaymentPolicy).HasConversion<int>().IsRequired();
        builder.Property(p => p.ProviderPaymentId).HasMaxLength(200);
        builder.Property(p => p.ProviderCustomerId).HasMaxLength(200);
        builder.Property(p => p.ProviderTransactionId).HasMaxLength(200);
        builder.Property(p => p.ProviderStatus).HasMaxLength(100);
        builder.Property(p => p.Reference).HasMaxLength(200);
        builder.Property(p => p.FailureReason).HasMaxLength(500);
        builder.Property(p => p.IdempotencyKey).HasMaxLength(150);
        builder.Property(p => p.MetadataJson).HasMaxLength(4000);
        builder.Property(p => p.RecordedByUserId);
        builder.Property(p => p.ConfirmedByUserId);
        builder.Property(p => p.ReceivedAtUtc);
        builder.Property(p => p.ConfirmedAtUtc);
        builder.Property(p => p.SettledAtUtc);
        builder.Property(p => p.CreatedAtUtc).IsRequired();
        builder.Property(p => p.UpdatedAtUtc);
        builder.Property(p => p.RowVersion).IsRowVersion();

        builder.Ignore(p => p.DomainEvents);
        builder.Ignore(p => p.RemainingRefundableAmount);
        builder.Ignore(p => p.PendingRefundAmount);
        builder.Ignore(p => p.AvailableToRefundWithoutReversingAllocations);

        builder.HasOne<Resident>()
            .WithMany()
            .HasForeignKey(p => p.ResidentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Lease>()
            .WithMany()
            .HasForeignKey(p => p.LeaseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<PaymentIntent>()
            .WithMany()
            .HasForeignKey(p => p.PaymentIntentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(p => p.Allocations)
            .WithOne()
            .HasForeignKey(a => a.PaymentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(p => p.Refunds)
            .WithOne()
            .HasForeignKey(r => r.PaymentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Navigation(p => p.Allocations).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(p => p.Refunds).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(p => new { p.OrganizationId, p.PaymentNumber })
            .IsUnique()
            .HasFilter("[PaymentNumber] IS NOT NULL");

        builder.HasIndex(p => new { p.OrganizationId, p.IdempotencyKey })
            .IsUnique()
            .HasFilter("[IdempotencyKey] IS NOT NULL");

        builder.HasIndex(p => new { p.Provider, p.ProviderPaymentId })
            .IsUnique()
            .HasFilter("[ProviderPaymentId] IS NOT NULL");

        builder.HasIndex(p => new { p.Provider, p.ProviderTransactionId })
            .IsUnique()
            .HasFilter("[ProviderTransactionId] IS NOT NULL");

        builder.HasIndex(p => new { p.OrganizationId, p.ResidentId, p.Status });
        builder.HasIndex(p => new { p.OrganizationId, p.Provider, p.CreatedAtUtc });
    }
}
