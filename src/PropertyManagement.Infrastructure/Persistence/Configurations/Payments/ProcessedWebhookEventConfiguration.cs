using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PropertyManagement.Domain.Entities.Payments;

namespace PropertyManagement.Infrastructure.Persistence.Configurations.Payments;

public sealed class ProcessedWebhookEventConfiguration : IEntityTypeConfiguration<ProcessedWebhookEvent>
{
    public void Configure(EntityTypeBuilder<ProcessedWebhookEvent> builder)
    {
        builder.ToTable("ProcessedWebhookEvents");

        builder.HasKey(w => w.Id);
        builder.Property(w => w.Id).ValueGeneratedNever();
        builder.Property(w => w.OrganizationId);
        builder.Property(w => w.Provider).HasConversion<int>().IsRequired();
        builder.Property(w => w.ProviderEventId).HasMaxLength(200).IsRequired();
        builder.Property(w => w.EventType).HasMaxLength(150).IsRequired();
        builder.Property(w => w.PayloadHash).HasMaxLength(128).IsRequired();
        builder.Property(w => w.Status).HasConversion<int>().IsRequired();
        builder.Property(w => w.EventTimestampUtc).IsRequired();
        builder.Property(w => w.ReceivedAtUtc).IsRequired();
        builder.Property(w => w.ProcessedAtUtc);
        builder.Property(w => w.FailureReason).HasMaxLength(1000);

        builder.HasIndex(w => new { w.Provider, w.ProviderEventId })
            .IsUnique();

        builder.HasIndex(w => new { w.Provider, w.Status, w.ReceivedAtUtc });
    }
}
