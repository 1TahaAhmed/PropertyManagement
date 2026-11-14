using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PropertyManagement.Domain.Entities.Billing;
using PropertyManagement.Domain.Entities.Leases;
using PropertyManagement.Domain.Entities.Properties;
using PropertyManagement.Domain.Entities.Residents;

namespace PropertyManagement.Infrastructure.Persistence.Configurations.Billing;

public sealed class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.ToTable("Invoices", t =>
        {
            t.HasCheckConstraint("CK_Invoices_BillingPeriod", "[BillingPeriodStart] <= [BillingPeriodEnd]");
            t.HasCheckConstraint("CK_Invoices_Dates", "[DueDate] >= [IssueDate]");
            t.HasCheckConstraint("CK_Invoices_Totals", "[Total] >= 0 AND [AmountPaid] >= 0 AND [BalanceDue] >= 0");
            t.HasCheckConstraint("CK_Invoices_IssuedNumber", "[Status] IN (1, 6) OR [InvoiceNumber] IS NOT NULL");
        });

        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).ValueGeneratedNever();

        builder.Property(i => i.OrganizationId).IsRequired();
        builder.Property(i => i.LeaseId).IsRequired();
        builder.Property(i => i.PropertyId).IsRequired();
        builder.Property(i => i.UnitId).IsRequired();
        builder.Property(i => i.ResidentId).IsRequired();

        builder.Property(i => i.InvoiceNumber).HasMaxLength(50);
        builder.Property(i => i.IdempotencyKey).HasMaxLength(150);

        builder.Property(i => i.BillingPeriodStart).IsRequired();
        builder.Property(i => i.BillingPeriodEnd).IsRequired();
        builder.Property(i => i.IssueDate).IsRequired();
        builder.Property(i => i.DueDate).IsRequired();

        builder.Property(i => i.Currency)
            .HasMaxLength(3)
            .IsRequired();

        builder.Property(i => i.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(i => i.Subtotal).HasPrecision(18, 2).IsRequired();
        builder.Property(i => i.TotalAdjustments).HasPrecision(18, 2).IsRequired();
        builder.Property(i => i.Total).HasPrecision(18, 2).IsRequired();
        builder.Property(i => i.AmountPaid).HasPrecision(18, 2).IsRequired();
        builder.Property(i => i.BalanceDue).HasPrecision(18, 2).IsRequired();

        builder.Property(i => i.Notes).HasMaxLength(2000);
        builder.Property(i => i.VoidReason).HasMaxLength(500);

        builder.Property(i => i.IssuedAtUtc);
        builder.Property(i => i.VoidedAtUtc);
        builder.Property(i => i.CreatedAtUtc).IsRequired();
        builder.Property(i => i.UpdatedAtUtc);
        builder.Property(i => i.RowVersion).IsRowVersion();

        builder.Ignore(i => i.BillingPeriod);
        builder.Ignore(i => i.DomainEvents);

        builder.HasOne<Lease>()
            .WithMany()
            .HasForeignKey(i => i.LeaseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Property>()
            .WithMany()
            .HasForeignKey(i => i.PropertyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Unit>()
            .WithMany()
            .HasForeignKey(i => i.UnitId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Resident>()
            .WithMany()
            .HasForeignKey(i => i.ResidentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(i => i.Lines)
            .WithOne()
            .HasForeignKey(l => l.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(i => i.Adjustments)
            .WithOne()
            .HasForeignKey(a => a.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(i => new { i.OrganizationId, i.InvoiceNumber })
            .IsUnique()
            .HasFilter("[InvoiceNumber] IS NOT NULL");

        builder.HasIndex(i => new { i.OrganizationId, i.IdempotencyKey })
            .IsUnique()
            .HasFilter("[IdempotencyKey] IS NOT NULL AND [Status] <> 6");

        builder.HasIndex(i => new { i.OrganizationId, i.LeaseId, i.BillingPeriodStart, i.BillingPeriodEnd });
        builder.HasIndex(i => new { i.OrganizationId, i.ResidentId, i.Status });
        builder.HasIndex(i => new { i.OrganizationId, i.Status, i.DueDate });
    }
}
