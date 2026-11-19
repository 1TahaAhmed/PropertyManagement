namespace PropertyManagement.Infrastructure.Persistence.Entities;

public sealed class JournalEntryNumberSequence
{
    private JournalEntryNumberSequence() { }

    public JournalEntryNumberSequence(Guid organizationId, int year, long nextValue)
    {
        OrganizationId = organizationId;
        Year = year;
        NextValue = nextValue;
    }

    public Guid OrganizationId { get; private set; }
    public int Year { get; private set; }
    public long NextValue { get; private set; }

    public void AdvanceTo(long nextValue) => NextValue = nextValue;
}
