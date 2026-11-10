namespace PropertyManagement.Domain.Entities.Leases;

public readonly record struct LeaseDateRange
{
    public DateOnly StartDate { get; }
    public DateOnly EndDate { get; }

    public LeaseDateRange(DateOnly startDate, DateOnly endDate)
    {
        if (startDate >= endDate)
        {
            throw new ArgumentException($"Lease StartDate ({startDate}) must be strictly earlier than EndDate ({endDate}).");
        }

        StartDate = startDate;
        EndDate = endDate;
    }

    /// <summary>
    /// Checks if this date range overlaps with another date range.
    /// Boundaries are inclusive: A [Jan 1, Jun 30] and B [Jul 1, Dec 31] do NOT overlap.
    /// A [Jan 1, Jul 1] and B [Jul 1, Dec 31] DO overlap on Jul 1.
    /// Overlap condition: max(StartA, StartB) <= min(EndA, EndB)
    /// </summary>
    public bool OverlapsWith(LeaseDateRange other)
    {
        var maxStart = StartDate > other.StartDate ? StartDate : other.StartDate;
        var minEnd = EndDate < other.EndDate ? EndDate : other.EndDate;

        return maxStart <= minEnd;
    }

    /// <summary>
    /// Checks if a specific date falls within the inclusive range [StartDate, EndDate].
    /// </summary>
    public bool Contains(DateOnly date)
    {
        return date >= StartDate && date <= EndDate;
    }
}
