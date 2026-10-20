using MediatR;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Application.Features.Units;
using PropertyManagement.Application.Features.Units.Response;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Application.Features.Units.Update;

public sealed record UpdateUnitCommand(
    Guid UnitId,
    string UnitNumber,
    UnitType Type,
    decimal MarketRent,
    int? FloorNumber,
    int Bedrooms,
    int Bathrooms,
    decimal SquareMeters,
    string Description = "") : IRequest<Result<UnitResponse>>;