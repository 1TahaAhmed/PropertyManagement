using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PropertyManagement.Domain.Entities.Ledger;

namespace PropertyManagement.Infrastructure.Persistence.Configurations.Ledger;

public sealed class JournalEntryConfiguration : IEntityTypeConfiguration<JournalEntry>
{
    public void Configure(EntityTypeBuilder<JournalEntry> builder)
    {
        builder.ToTable("JournalEntries", t =>
        {
            t.HasCheckConstraint("CK_JournalEntries_Totals",
                "CAST([TotalDebits] AS DECIMAL(18,2)) >= 0 AND CAST([TotalCredits] AS DECIMAL(18,2)) >= 0 AND ([Status] = 1 OR CAST([TotalDebits] AS DECIMAL(18,2)) = CAST([TotalCredits] AS DECIMAL(18,2)))");
        });

        builder.HasKey(j => j.Id);
        builder.Property(j => j.Id).ValueGeneratedNever();
        builder.Property(j => j.OrganizationId).IsRequired();
        builder.Property(j => j.EntryNumber).HasMaxLength(50).IsRequired();
        builder.Property(j => j.SourceType).HasMaxLength(100).IsRequired();
        builder.Property(j => j.SourceId).IsRequired();
        builder.Property(j => j.IdempotencyKey).HasMaxLength(150).IsRequired();
        builder.Property(j => j.Currency).HasMaxLength(3).IsRequired();
        builder.Property(j => j.Description).HasMaxLength(500).IsRequired();
        builder.Property(j => j.Status).HasConversion<int>().IsRequired();
        builder.Property(j => j.TotalDebits).HasPrecision(18, 2).IsRequired();
        builder.Property(j => j.TotalCredits).HasPrecision(18, 2).IsRequired();
        builder.Property(j => j.EntryDateUtc).IsRequired();
        builder.Property(j => j.CreatedAtUtc).IsRequired();
        builder.Property(j => j.PostedAtUtc);
        builder.Property(j => j.RowVersion).IsRowVersion();

        builder.Ignore(j => j.DomainEvents);

        builder.HasMany(j => j.Lines)
            .WithOne()
            .HasForeignKey(l => l.JournalEntryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Navigation(j => j.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(j => new { j.OrganizationId, j.EntryNumber }).IsUnique();
        builder.HasIndex(j => new { j.OrganizationId, j.SourceType, j.SourceId, j.IdempotencyKey }).IsUnique();
        builder.HasIndex(j => new { j.OrganizationId, j.EntryDateUtc });
    }
}
