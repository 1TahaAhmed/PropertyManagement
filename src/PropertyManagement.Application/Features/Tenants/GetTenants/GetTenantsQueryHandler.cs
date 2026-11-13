using MediatR;
using PropertyManagement.Application.Abstractions.Tenants;
using PropertyManagement.Application.Common.Models;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Application.Features.Tenants.Responses;

namespace PropertyManagement.Application.Features.Tenants.GetTenants;

public sealed class GetTenantsQueryHandler(
    ITenantRepository tenantRepository)
    : IRequestHandler<GetTenantsQuery, Result<PagedList<TenantResponse>>>
{
    public async Task<Result<PagedList<TenantResponse>>> Handle(
        GetTenantsQuery request,
        CancellationToken cancellationToken)
    {
        var (items, totalCount) = await tenantRepository.GetPagedAsync(
            request.OrganizationId,
            request.SearchTerm,
            request.Status,
            request.PageNumber,
            request.PageSize,
            cancellationToken);

        var responseItems = items.Select(t => t.ToResponse()).ToList();

        return Result<PagedList<TenantResponse>>.Success(
            new PagedList<TenantResponse>(responseItems, request.PageNumber, request.PageSize, totalCount));
    }
}
