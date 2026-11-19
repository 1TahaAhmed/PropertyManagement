using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PropertyManagement.Domain.Entities.Payments;

namespace PropertyManagement.Infrastructure.Persistence.Configurations.Payments;

public sealed class PaymentReconciliationRecordConfiguration : IEntityTypeConfiguration<PaymentReconciliationRecord>
{
    public void Configure(EntityTypeBuilder<PaymentReconciliationRecord> builder)
    {
        builder.ToTable("PaymentReconciliationRecords");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();
        builder.Property(r => r.OrganizationId).IsRequired();
        builder.Property(r => r.BatchId).IsRequired();
        builder.Property(r => r.Provider).HasConversion<int>().IsRequired();
        builder.Property(r => r.InternalPaymentId);
        builder.Property(r => r.ProviderTransactionId).HasMaxLength(200);
        builder.Property(r => r.Currency).HasMaxLength(3).IsRequired();
        builder.Property(r => r.ExpectedAmount).HasPrecision(18, 2);
        builder.Property(r => r.ProviderAmount).HasPrecision(18, 2);
        builder.Property(r => r.ExpectedFee).HasPrecision(18, 2);
        builder.Property(r => r.ProviderFee).HasPrecision(18, 2);
        builder.Property(r => r.ExpectedNetAmount).HasPrecision(18, 2);
        builder.Property(r => r.ProviderNetAmount).HasPrecision(18, 2);
        builder.Property(r => r.Status).HasConversion<int>().IsRequired();
        builder.Property(r => r.MismatchDetails).HasMaxLength(1000);
        builder.Property(r => r.ResolutionNotes).HasMaxLength(1000);
        builder.Property(r => r.ResolvedByUserId);
        builder.Property(r => r.ResolvedAtUtc);
        builder.Property(r => r.CreatedAtUtc).IsRequired();

        builder.Ignore(r => r.DomainEvents);

        builder.HasOne<Payment>()
            .WithMany()
            .HasForeignKey(r => r.InternalPaymentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(r => new { r.OrganizationId, r.BatchId });
        builder.HasIndex(r => new { r.OrganizationId, r.Status, r.CreatedAtUtc });
    }
}
