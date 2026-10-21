using MediatR;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Application.Features.Units.Response;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Application.Features.Units.Update;

public sealed record UpdateUnitCommand(
    Guid UnitId,
    string UnitNumber,
    UnitType Type,
    decimal MarketRent,
    int? FloorNumber = null,
    int Bedrooms = 0,
    int Bathrooms = 0,
    decimal SquareMeters = 0,
    string Description = "",
    string? RowVersion = null) : IRequest<Result<UnitResponse>>;