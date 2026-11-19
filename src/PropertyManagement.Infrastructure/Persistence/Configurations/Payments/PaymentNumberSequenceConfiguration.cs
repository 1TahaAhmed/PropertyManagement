using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PropertyManagement.Domain.Entities.Organizations;
using PropertyManagement.Infrastructure.Persistence.Entities;

namespace PropertyManagement.Infrastructure.Persistence.Configurations.Payments;

public sealed class PaymentNumberSequenceConfiguration : IEntityTypeConfiguration<PaymentNumberSequence>
{
    public void Configure(EntityTypeBuilder<PaymentNumberSequence> builder)
    {
        builder.ToTable("PaymentNumberSequences");
        builder.HasKey(s => new { s.OrganizationId, s.Year });
        builder.Property(s => s.OrganizationId).IsRequired();
        builder.Property(s => s.Year).IsRequired();
        builder.Property(s => s.NextValue).IsRequired();

        builder.HasOne<Organization>()
            .WithMany()
            .HasForeignKey(s => s.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
