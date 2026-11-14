using MediatR;
using PropertyManagement.Application.Abstractions.Residents;
using PropertyManagement.Application.Common.Models;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Application.Features.Residents.Responses;

namespace PropertyManagement.Application.Features.Residents.GetResidents;

public sealed class GetResidentsQueryHandler(
    IResidentRepository residentRepository)
    : IRequestHandler<GetResidentsQuery, Result<PagedList<ResidentSummaryResponse>>>
{
    public async Task<Result<PagedList<ResidentSummaryResponse>>> Handle(
        GetResidentsQuery request,
        CancellationToken cancellationToken)
    {
        var (items, totalCount) = await residentRepository.GetPagedAsync(
            request.OrganizationId,
            request.SearchTerm,
            request.Status,
            request.PageNumber,
            request.PageSize,
            cancellationToken,
            request.LeaseRole,
            request.OnlyLeaseHolders);

        var responseItems = items.Select(r => r.ToSummaryResponse()).ToList();

        return Result<PagedList<ResidentSummaryResponse>>.Success(
            new PagedList<ResidentSummaryResponse>(responseItems, request.PageNumber, request.PageSize, totalCount));
    }
}
