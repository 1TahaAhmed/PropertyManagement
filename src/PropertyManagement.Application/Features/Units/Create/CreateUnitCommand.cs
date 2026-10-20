using MediatR;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Application.Features.Units.Response;
using PropertyManagement.Domain.Enums;

public sealed record CreateUnitCommand(
    Guid TenantId,
    Guid PropertyId,
    string UnitNumber,
    UnitType Type,
    decimal MarketRent,
    int? FloorNumber = null,
    int Bedrooms = 0,
    int Bathrooms = 0,
    decimal SquareMeters = 0,
    string Description = "") : IRequest<Result<UnitResponse>>;