namespace PropertyManagement.Domain.Entities.Leases;

public sealed record LeaseTerminationDetails
{
    public DateOnly TerminationDate { get; init; }
    public string Reason { get; init; }
    public string? Notes { get; init; }
    public Guid TerminatedByUserId { get; init; }
    public DateTime TerminatedAtUtc { get; init; }

    public LeaseTerminationDetails(
        DateOnly terminationDate,
        string reason,
        Guid terminatedByUserId,
        DateTime terminatedAtUtc,
        string? notes = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason, nameof(reason));

        if (terminatedByUserId == Guid.Empty)
        {
            throw new ArgumentException("Terminating user ID cannot be empty.", nameof(terminatedByUserId));
        }

        TerminationDate = terminationDate;
        Reason = reason.Trim();
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        TerminatedByUserId = terminatedByUserId;
        TerminatedAtUtc = terminatedAtUtc;
    }
}
