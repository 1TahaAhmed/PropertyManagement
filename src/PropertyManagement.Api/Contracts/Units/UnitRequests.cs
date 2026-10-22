using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Api.Contracts.Units;

public sealed record CreateUnitRequest(
    Guid TenantId,
    string UnitNumber,
    UnitType Type,
    decimal MarketRent,
    int? FloorNumber = null,
    int Bedrooms = 0,
    int Bathrooms = 0,
    decimal SquareMeters = 0,
    string Description = "");

public sealed record UpdateUnitRequest(
    Guid TenantId,
    string UnitNumber,
    UnitType Type,
    decimal MarketRent,
    int? FloorNumber = null,
    int Bedrooms = 0,
    int Bathrooms = 0,
    decimal SquareMeters = 0,
    string Description = "",
    string? RowVersion = null);

public sealed record ChangeUnitStatusRequest(UnitOperationalStatus NewStatus);