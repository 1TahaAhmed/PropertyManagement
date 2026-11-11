using MediatR;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Application.Features.Leases.Responses;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Application.Features.Leases.CreateDraft;

public sealed record CreateDraftLeaseCommand(
    Guid OrganizationId,
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
    Guid? PrimaryResidentId = null) : IRequest<Result<LeaseResponse>>;
