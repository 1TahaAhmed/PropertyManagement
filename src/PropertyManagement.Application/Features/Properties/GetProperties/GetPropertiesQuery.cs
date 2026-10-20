using MediatR;
using PropertyManagement.Application.Abstractions.Properties;
using PropertyManagement.Application.Common.Models;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Application.Features.Properties.Response;
using PropertyManagement.Domain.Enums;

public sealed record GetPropertiesQuery(
    Guid TenantId,
    string? SearchTerm = null,
    PropertyStatus? Status = null,
    PropertyType? Type = null,
    int PageNumber = 1,
    int PageSize = 10) : IRequest<Result<PagedList<PropertyResponse>>>;

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
            p.UpdatedAt)).ToList();

        return Result<PagedList<PropertyResponse>>.Success(
            new PagedList<PropertyResponse>(responseItems, request.PageNumber, request.PageSize, totalCount));
    }
}