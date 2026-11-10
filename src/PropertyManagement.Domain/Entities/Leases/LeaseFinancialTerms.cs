using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Domain.Entities.Leases;

public sealed record LeaseFinancialTerms
{
    public decimal RentAmount { get; init; }
    public decimal SecurityDeposit { get; init; }
    public RentFrequency Frequency { get; init; }
    public int DueDayOfMonth { get; init; }
    public int GracePeriodDays { get; init; }
    public decimal LateFeePercentage { get; init; }

    public LeaseFinancialTerms(
        decimal rentAmount,
        decimal securityDeposit,
        RentFrequency frequency = RentFrequency.Monthly,
        int dueDayOfMonth = 1,
        int gracePeriodDays = 5,
        decimal lateFeePercentage = 0m)
    {
        if (rentAmount <= 0)
        {
            throw new ArgumentException("Rent amount must be greater than zero.", nameof(rentAmount));
        }

        if (securityDeposit < 0)
        {
            throw new ArgumentException("Security deposit cannot be negative.", nameof(securityDeposit));
        }

        if (dueDayOfMonth is < 1 or > 28)
        {
            throw new ArgumentException("Due day of month must be between 1 and 28.", nameof(dueDayOfMonth));
        }

        if (gracePeriodDays < 0)
        {
            throw new ArgumentException("Grace period days cannot be negative.", nameof(gracePeriodDays));
        }

        if (lateFeePercentage is < 0 or > 100)
        {
            throw new ArgumentException("Late fee percentage must be between 0 and 100.", nameof(lateFeePercentage));
        }

        RentAmount = rentAmount;
        SecurityDeposit = securityDeposit;
        Frequency = frequency;
        DueDayOfMonth = dueDayOfMonth;
        GracePeriodDays = gracePeriodDays;
        LateFeePercentage = lateFeePercentage;
    }
}
