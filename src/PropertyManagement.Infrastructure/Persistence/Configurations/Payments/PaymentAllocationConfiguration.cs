using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PropertyManagement.Domain.Entities.Billing;
using PropertyManagement.Domain.Entities.Payments;

namespace PropertyManagement.Infrastructure.Persistence.Configurations.Payments;

public sealed class PaymentAllocationConfiguration : IEntityTypeConfiguration<PaymentAllocation>
{
    public void Configure(EntityTypeBuilder<PaymentAllocation> builder)
    {
        builder.ToTable("PaymentAllocations", t =>
        {
            t.HasCheckConstraint("CK_PaymentAllocations_Amount", "[Amount] > 0");
        });

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();
        builder.Property(a => a.OrganizationId).IsRequired();
        builder.Property(a => a.PaymentId).IsRequired();
        builder.Property(a => a.InvoiceId).IsRequired();
        builder.Property(a => a.Amount).HasPrecision(18, 2).IsRequired();
        builder.Property(a => a.Currency).HasMaxLength(3).IsRequired();
        builder.Property(a => a.AllocatedAtUtc).IsRequired();
        builder.Property(a => a.AllocatedByUserId);
        builder.Property(a => a.IsReversed).IsRequired();
        builder.Property(a => a.ReversedAtUtc);
        builder.Property(a => a.ReversalReason).HasMaxLength(500);
        builder.Property(a => a.ReversedByUserId);

        builder.HasOne<Invoice>()
            .WithMany()
            .HasForeignKey(a => a.InvoiceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(a => new { a.OrganizationId, a.PaymentId, a.IsReversed });
        builder.HasIndex(a => new { a.OrganizationId, a.InvoiceId, a.IsReversed });
    }
}
