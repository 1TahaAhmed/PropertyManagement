using MediatR;
using PropertyManagement.Application.Abstractions.Properties;
using PropertyManagement.Application.Common.Enums;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Application.Features.Units.Response;

namespace PropertyManagement.Application.Features.Units.GetById;

public sealed class GetUnitByIdQueryHandler(IUnitRepository unitRepository)
    : IRequestHandler<GetUnitByIdQuery, Result<UnitResponse>>
{
    public async Task<Result<UnitResponse>> Handle(GetUnitByIdQuery request, CancellationToken cancellationToken)
    {
        var unit = await unitRepository.GetByIdAsync(request.UnitId, request.OrganizationId, cancellationToken);
        if (unit is null)
        {
            return Result<UnitResponse>.Failure(new Error(
                Code: "Unit.NotFound",
                Message: "Unit was not found.",
                Type: ErrorType.NotFound));
        }

        return Result<UnitResponse>.Success(new UnitResponse(
            unit.Id,
            unit.OrganizationId,
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
            unit.UpdatedAt,
            Convert.ToBase64String(unit.RowVersion)));
    }
}
