using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Api.Contracts.Leases;

public sealed record RenewLeaseRequest(
    DateOnly NewEndDate,
    decimal? NewRentAmount = null,
    decimal? NewSecurityDeposit = null,
    RentFrequency? NewFrequency = null,
    int? NewDueDayOfMonth = null,
    int? NewGracePeriodDays = null,
    decimal? NewLateFeePercentage = null);
