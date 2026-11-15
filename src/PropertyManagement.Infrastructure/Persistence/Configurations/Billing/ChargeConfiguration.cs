using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PropertyManagement.Domain.Entities.Billing;
using PropertyManagement.Domain.Entities.Leases;
using PropertyManagement.Domain.Entities.Properties;
using PropertyManagement.Domain.Entities.Residents;

namespace PropertyManagement.Infrastructure.Persistence.Configurations.Billing;

public sealed class ChargeConfiguration : IEntityTypeConfiguration<Charge>
{
    public void Configure(EntityTypeBuilder<Charge> builder)
    {
        builder.ToTable("Charges", t =>
        {
            t.HasCheckConstraint("CK_Charges_Quantity", "[Quantity] > 0");
            t.HasCheckConstraint("CK_Charges_UnitPrice", "[UnitPrice] > 0");
            t.HasCheckConstraint("CK_Charges_Amount", "[Amount] <> 0");
            t.HasCheckConstraint(
                "CK_Charges_Period",
                "([PeriodStart] IS NULL AND [PeriodEnd] IS NULL) OR ([PeriodStart] IS NOT NULL AND [PeriodEnd] IS NOT NULL AND [PeriodStart] <= [PeriodEnd])");
        });

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.Property(c => c.OrganizationId).IsRequired();
        builder.Property(c => c.LeaseId).IsRequired();
        builder.Property(c => c.PropertyId).IsRequired();
        builder.Property(c => c.UnitId).IsRequired();
        builder.Property(c => c.ResidentId).IsRequired();

        builder.Property(c => c.Type).HasConversion<int>().IsRequired();
        builder.Property(c => c.Status).HasConversion<int>().IsRequired();
        builder.Property(c => c.Description).HasMaxLength(500).IsRequired();
        builder.Property(c => c.Quantity).HasPrecision(18, 4).IsRequired();
        builder.Property(c => c.UnitPrice).HasPrecision(18, 2).IsRequired();
        builder.Property(c => c.Amount).HasPrecision(18, 2).IsRequired();
        builder.Property(c => c.Currency).HasMaxLength(3).IsRequired();
        builder.Property(c => c.PeriodStart);
        builder.Property(c => c.PeriodEnd);
        builder.Property(c => c.InvoiceId);
        builder.Property(c => c.VoidReason).HasMaxLength(500);
        builder.Property(c => c.CreatedAtUtc).IsRequired();
        builder.Property(c => c.UpdatedAtUtc);
        builder.Property(c => c.RowVersion).IsRowVersion();

        builder.HasOne<Lease>()
            .WithMany()
            .HasForeignKey(c => c.LeaseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Property>()
            .WithMany()
            .HasForeignKey(c => c.PropertyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Unit>()
            .WithMany()
            .HasForeignKey(c => c.UnitId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Resident>()
            .WithMany()
            .HasForeignKey(c => c.ResidentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Invoice>()
            .WithMany()
            .HasForeignKey(c => c.InvoiceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(c => new { c.OrganizationId, c.LeaseId, c.Status });
        builder.HasIndex(c => new { c.OrganizationId, c.InvoiceId });
    }
}
