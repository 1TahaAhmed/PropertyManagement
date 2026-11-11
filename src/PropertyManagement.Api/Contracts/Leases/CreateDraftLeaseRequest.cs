using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Api.Contracts.Leases;

public sealed record CreateDraftLeaseRequest(
    Guid PropertyId,
    Guid UnitId,
    DateOnly StartDate,
    DateOnly EndDate,
    decimal RentAmount,
    decimal SecurityDeposit,
    RentFrequency Frequency = RentFrequency.Monthly,
    int DueDayOfMonth = 1,
    int GracePeriodDays = 5,
    decimal LateFeePercentage = 0m,
    Guid? PrimaryResidentId = null);
