using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PropertyManagement.Domain.Entities.Ledger;

namespace PropertyManagement.Infrastructure.Persistence.Configurations.Ledger;

public sealed class JournalLineConfiguration : IEntityTypeConfiguration<JournalLine>
{
    public void Configure(EntityTypeBuilder<JournalLine> builder)
    {
        builder.ToTable("JournalLines", t =>
        {
            t.HasCheckConstraint("CK_JournalLines_Amounts",
                "CAST([Debit] AS DECIMAL(18,2)) >= 0 AND CAST([Credit] AS DECIMAL(18,2)) >= 0 AND ((CAST([Debit] AS DECIMAL(18,2)) > 0 AND CAST([Credit] AS DECIMAL(18,2)) = 0) OR (CAST([Credit] AS DECIMAL(18,2)) > 0 AND CAST([Debit] AS DECIMAL(18,2)) = 0))");
        });

        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).ValueGeneratedNever();
        builder.Property(l => l.OrganizationId).IsRequired();
        builder.Property(l => l.JournalEntryId).IsRequired();
        builder.Property(l => l.AccountCode).HasConversion<int>().IsRequired();
        builder.Property(l => l.AccountType).HasConversion<int>().IsRequired();
        builder.Property(l => l.Debit).HasPrecision(18, 2).IsRequired();
        builder.Property(l => l.Credit).HasPrecision(18, 2).IsRequired();
        builder.Property(l => l.Currency).HasMaxLength(3).IsRequired();
        builder.Property(l => l.Description).HasMaxLength(500).IsRequired();

        builder.HasIndex(l => new { l.OrganizationId, l.AccountCode });
        builder.HasIndex(l => l.JournalEntryId);
    }
}
