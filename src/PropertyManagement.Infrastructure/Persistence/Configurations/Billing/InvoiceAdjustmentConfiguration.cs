using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PropertyManagement.Domain.Entities.Billing;

namespace PropertyManagement.Infrastructure.Persistence.Configurations.Billing;

public sealed class InvoiceAdjustmentConfiguration : IEntityTypeConfiguration<InvoiceAdjustment>
{
    public void Configure(EntityTypeBuilder<InvoiceAdjustment> builder)
    {
        builder.ToTable("InvoiceAdjustments", t =>
        {
            t.HasCheckConstraint("CK_InvoiceAdjustments_Amount", "[Amount] > 0");
        });

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();

        builder.Property(a => a.OrganizationId).IsRequired();
        builder.Property(a => a.InvoiceId).IsRequired();
        builder.Property(a => a.Type).HasConversion<int>().IsRequired();
        builder.Property(a => a.ChargeType).HasConversion<int>().IsRequired();
        builder.Property(a => a.Description).HasMaxLength(500).IsRequired();
        builder.Property(a => a.Reason).HasMaxLength(500).IsRequired();
        builder.Property(a => a.Amount).HasPrecision(18, 2).IsRequired();
        builder.Property(a => a.CreatedByUserId).IsRequired();
        builder.Property(a => a.CreatedAtUtc).IsRequired();

        builder.Ignore(a => a.SignedAmount);

        builder.HasIndex(a => new { a.OrganizationId, a.InvoiceId });
    }
}
