using MediatR;
using PropertyManagement.Application.Abstractions.Repositories;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Application.Features.Leases.Responses;

namespace PropertyManagement.Application.Features.Leases.GetPaged;

public sealed class GetLeasesPagedQueryHandler(ILeaseRepository leaseRepository)
    : IRequestHandler<GetLeasesPagedQuery, Result<PagedLeaseResponse>>
{
    public async Task<Result<PagedLeaseResponse>> Handle(GetLeasesPagedQuery request, CancellationToken cancellationToken)
    {
        var (items, totalCount) = await leaseRepository.GetPagedAsync(
            request.OrganizationId,
            request.PropertyId,
            request.UnitId,
            request.Status,
            request.PageNumber,
            request.PageSize,
            cancellationToken);

        var response = new PagedLeaseResponse(
            items.Select(LeaseResponse.FromEntity).ToList(),
            totalCount,
            request.PageNumber,
            request.PageSize);

        return Result<PagedLeaseResponse>.Success(response);
    }
}
