using MediatR;
using PropertyManagement.Application.Abstractions.Properties;
using PropertyManagement.Application.Common.Enums;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Application.Features.Units.Response;

public sealed record GetUnitByIdQuery(Guid UnitId) : IRequest<Result<UnitResponse>>;

public sealed class GetUnitByIdQueryHandler(IUnitRepository unitRepository)
    : IRequestHandler<GetUnitByIdQuery, Result<UnitResponse>>
{
    public async Task<Result<UnitResponse>> Handle(GetUnitByIdQuery request, CancellationToken cancellationToken)
    {
        var unit = await unitRepository.GetByIdAsync(request.UnitId, cancellationToken);
        if (unit is null)
        {
            return Result<UnitResponse>.Failure(new Error(
                Code: "Unit.NotFound",
                Message: "Unit was not found.",
                Type: ErrorType.NotFound));
        }

        return Result<UnitResponse>.Success(new UnitResponse(
            unit.Id,
            unit.TenantId,
            unit.PropertyId,
            unit.UnitNumber,
            unit.FloorNumber,
            unit.Type,
            unit.Status,
            unit.MarketRent,
            unit.Bedrooms,
            unit.Bathrooms,
            unit.SquareMeters,
            unit.Description,
            unit.CreatedAt,
            unit.UpdatedAt));
    }
}