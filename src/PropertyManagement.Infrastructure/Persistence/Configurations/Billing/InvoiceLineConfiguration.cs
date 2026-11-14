using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PropertyManagement.Domain.Entities.Billing;

namespace PropertyManagement.Infrastructure.Persistence.Configurations.Billing;

public sealed class InvoiceLineConfiguration : IEntityTypeConfiguration<InvoiceLine>
{
    public void Configure(EntityTypeBuilder<InvoiceLine> builder)
    {
        builder.ToTable("InvoiceLines", t =>
        {
            t.HasCheckConstraint("CK_InvoiceLines_Quantity", "[Quantity] > 0");
            t.HasCheckConstraint("CK_InvoiceLines_UnitPrice", "[UnitPrice] > 0");
            t.HasCheckConstraint("CK_InvoiceLines_Amount", "[Amount] <> 0");
        });

        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).ValueGeneratedNever();

        builder.Property(l => l.OrganizationId).IsRequired();
        builder.Property(l => l.InvoiceId).IsRequired();
        builder.Property(l => l.Type).HasConversion<int>().IsRequired();
        builder.Property(l => l.Description).HasMaxLength(500).IsRequired();
        builder.Property(l => l.Quantity).HasPrecision(18, 4).IsRequired();
        builder.Property(l => l.UnitPrice).HasPrecision(18, 2).IsRequired();
        builder.Property(l => l.Amount).HasPrecision(18, 2).IsRequired();
        builder.Property(l => l.SourceChargeId);
        builder.Property(l => l.PeriodStart);
        builder.Property(l => l.PeriodEnd);
        builder.Property(l => l.CreatedAtUtc).IsRequired();

        builder.HasOne<Charge>()
            .WithMany()
            .HasForeignKey(l => l.SourceChargeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(l => new { l.OrganizationId, l.InvoiceId });
        builder.HasIndex(l => l.SourceChargeId).HasFilter("[SourceChargeId] IS NOT NULL");
    }
}
