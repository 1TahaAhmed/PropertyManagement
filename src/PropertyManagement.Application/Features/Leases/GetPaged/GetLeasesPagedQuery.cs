using MediatR;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Application.Features.Leases.Responses;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Application.Features.Leases.GetPaged;

public sealed record PagedLeaseResponse(
    IReadOnlyList<LeaseResponse> Items,
    int TotalCount,
    int PageNumber,
    int PageSize);

public sealed record GetLeasesPagedQuery(
    Guid OrganizationId,
    Guid? PropertyId = null,
    Guid? UnitId = null,
    LeaseStatus? Status = null,
    int PageNumber = 1,
    int PageSize = 20) : IRequest<Result<PagedLeaseResponse>>;
