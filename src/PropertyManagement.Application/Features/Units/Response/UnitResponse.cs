using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Application.Features.Units.Response;

public sealed record UnitResponse(
    Guid Id,
    Guid TenantId,
    Guid PropertyId,
    string UnitNumber,
    int? FloorNumber,
    UnitType Type,
    UnitOperationalStatus Status,
    decimal MarketRent,
    int Bedrooms,
    int Bathrooms,
    decimal SquareMeters,
    string Description,
    DateTime CreatedAt,
    DateTime? UpdatedAt);