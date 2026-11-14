namespace PropertyManagement.Domain.Entities.Billing;

public readonly record struct BillingPeriod
{
    public DateOnly StartDate { get; }
    public DateOnly EndDate { get; }

    public BillingPeriod(DateOnly startDate, DateOnly endDate)
    {
        if (startDate > endDate)
        {
            throw new ArgumentException($"Billing period StartDate ({startDate}) cannot be later than EndDate ({endDate}).");
        }

        StartDate = startDate;
        EndDate = endDate;
    }

    public int TotalDays => EndDate.DayNumber - StartDate.DayNumber + 1;

    public bool OverlapsWith(BillingPeriod other) =>
        StartDate <= other.EndDate && other.StartDate <= EndDate;

    public BillingPeriod? Intersect(DateOnly rangeStart, DateOnly rangeEnd)
    {
        if (rangeStart > rangeEnd)
        {
            return null;
        }

        var overlapStart = StartDate > rangeStart ? StartDate : rangeStart;
        var overlapEnd = EndDate < rangeEnd ? EndDate : rangeEnd;

        return overlapStart <= overlapEnd
            ? new BillingPeriod(overlapStart, overlapEnd)
            : null;
    }

    public static BillingPeriod ForMonth(int year, int month)
    {
        var start = new DateOnly(year, month, 1);
        var end = new DateOnly(year, month, DateTime.DaysInMonth(year, month));
        return new BillingPeriod(start, end);
    }
}
