using MediatR;
using PropertyManagement.Application.Abstractions.Properties;
using PropertyManagement.Application.Common.Models;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Application.Features.Properties;
using PropertyManagement.Application.Features.Properties.Response;

namespace PropertyManagement.Application.Features.Properties.GetProperties;

public sealed class GetPropertiesQueryHandler(IPropertyRepository propertyRepository)
    : IRequestHandler<GetPropertiesQuery, Result<PagedList<PropertyResponse>>>
{
    public async Task<Result<PagedList<PropertyResponse>>> Handle(GetPropertiesQuery request, CancellationToken cancellationToken)
    {
        var (items, totalCount) = await propertyRepository.GetPagedAsync(
            request.TenantId,
            request.SearchTerm,
            request.Status,
            request.Type,
            request.PageNumber,
            request.PageSize,
            cancellationToken);

        var responseItems = items.Select(p => new PropertyResponse(
            p.Id,
            p.TenantId,
            p.Name,
            p.Address,
            p.Description,
            p.Type,
            p.Status,
            p.CreatedAt,
            p.UpdatedAt,
            Convert.ToBase64String(p.RowVersion))).ToList();

        return Result<PagedList<PropertyResponse>>.Success(
            new PagedList<PropertyResponse>(responseItems, request.PageNumber, request.PageSize, totalCount));
    }
}