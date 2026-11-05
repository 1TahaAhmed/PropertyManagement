using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Api.Contracts.Units;

public sealed record CreateUnitRequest(
    string UnitNumber,
    UnitType Type,
    decimal MarketRent,
    int? FloorNumber = null,
    int Bedrooms = 0,
    int Bathrooms = 0,
    decimal SquareMeters = 0,
    string Description = "",
    Guid OrganizationId = default);

public sealed record UpdateUnitRequest(
    string UnitNumber,
    string RowVersion,
    UnitType Type,
    decimal MarketRent,
    int? FloorNumber = null,
    int Bedrooms = 0,
    int Bathrooms = 0,
    decimal SquareMeters = 0,
    string Description = "",
    Guid OrganizationId = default);

public sealed record ChangeUnitStatusRequest(
    UnitOperationalStatus NewStatus,
    Guid OrganizationId = default);
