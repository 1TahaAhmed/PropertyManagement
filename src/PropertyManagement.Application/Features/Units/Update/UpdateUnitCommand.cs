using MediatR;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Application.Features.Units.Response;
using PropertyManagement.Domain.Enums;

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