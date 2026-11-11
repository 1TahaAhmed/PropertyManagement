using MediatR;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Application.Features.Leases.Responses;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Application.Features.Leases.Renew;

public sealed record RenewLeaseCommand(
    Guid OrganizationId,
    Guid LeaseId,
    DateOnly NewEndDate,
    decimal? NewRentAmount = null,
    decimal? NewSecurityDeposit = null,
    RentFrequency? NewFrequency = null,
    int? NewDueDayOfMonth = null,
    int? NewGracePeriodDays = null,
    decimal? NewLateFeePercentage = null) : IRequest<Result<LeaseResponse>>;
