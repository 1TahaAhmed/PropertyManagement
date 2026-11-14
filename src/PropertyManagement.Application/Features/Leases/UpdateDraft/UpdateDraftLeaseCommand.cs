using MediatR;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Application.Features.Leases.Responses;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Application.Features.Leases.UpdateDraft;

public sealed record UpdateDraftLeaseCommand(
    Guid OrganizationId,
    Guid LeaseId,
    DateOnly StartDate,
    DateOnly EndDate,
    decimal RentAmount,
    decimal SecurityDeposit,
    RentFrequency Frequency = RentFrequency.Monthly,
    int DueDayOfMonth = 1,
    int GracePeriodDays = 5,
    decimal LateFeePercentage = 0m,
    string? RowVersion = null) : IRequest<Result<LeaseResponse>>;
