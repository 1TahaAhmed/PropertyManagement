using MediatR;
using PropertyManagement.Application.Abstractions.Properties;
using PropertyManagement.Application.Common.Models;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Application.Features.Units.Response;
using PropertyManagement.Domain.Enums;

public sealed record GetUnitsByPropertyQuery(
    Guid PropertyId,
    UnitOperationalStatus? Status = null,
    UnitType? Type = null,
    decimal? MinRent = null,
    decimal? MaxRent = null,
    int PageNumber = 1,
    int PageSize = 10) : IRequest<Result<PagedList<UnitResponse>>>;

public sealed class GetUnitsByPropertyQueryHandler(IUnitRepository unitRepository)
    : IRequestHandler<GetUnitsByPropertyQuery, Result<PagedList<UnitResponse>>>
{
    public async Task<Result<PagedList<UnitResponse>>> Handle(GetUnitsByPropertyQuery request, CancellationToken cancellationToken)
    {
        var (items, totalCount) = await unitRepository.GetPagedByPropertyAsync(
            request.PropertyId,
            request.Status,
            request.Type,
            request.MinRent,
            request.MaxRent,
            request.PageNumber,
            request.PageSize,
            cancellationToken);

        var responseItems = items.Select(u => new UnitResponse(
            u.Id,
            u.TenantId,
            u.PropertyId,
            u.UnitNumber,
            u.FloorNumber,
            u.Type,
            u.Status,
            u.MarketRent,
            u.Bedrooms,
            u.Bathrooms,
            u.SquareMeters,
            u.Description,
            u.CreatedAt,
            u.UpdatedAt)).ToList();

        return Result<PagedList<UnitResponse>>.Success(
            new PagedList<UnitResponse>(responseItems, request.PageNumber, request.PageSize, totalCount));
    }
}
