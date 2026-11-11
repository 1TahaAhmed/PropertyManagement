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

    public bool OverlapsWith(LeaseDateRange other)
    {
        var maxStart = StartDate > other.StartDate ? StartDate : other.StartDate;
        var minEnd = EndDate < other.EndDate ? EndDate : other.EndDate;

        return maxStart <= minEnd;
    }

    public bool Contains(DateOnly date)
    {
        return date >= StartDate && date <= EndDate;
    }
}
