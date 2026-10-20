using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Domain.Entities.Properties;

public sealed class Unit
{
    private Unit() { } 

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid PropertyId { get; private set; }
    public string UnitNumber { get; private set; } = string.Empty;
    public int? FloorNumber { get; private set; }
    public UnitType Type { get; private set; }
    public UnitOperationalStatus Status { get; private set; }
    public decimal MarketRent { get; private set; }
    public int Bedrooms { get; private set; }
    public int Bathrooms { get; private set; }
    public decimal SquareMeters { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public static Unit Create(
        Guid tenantId,
        Guid propertyId,
        string unitNumber,
        UnitType type,
        decimal marketRent,
        DateTime utcNow,
        int? floorNumber = null,
        int bedrooms = 0,
        int bathrooms = 0,
        decimal squareMeters = 0,
        string description = "")
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("Tenant ID cannot be empty.", nameof(tenantId));
        }

        if (propertyId == Guid.Empty)
        {
            throw new ArgumentException("Property ID cannot be empty.", nameof(propertyId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(unitNumber, nameof(unitNumber));

        if (marketRent < 0)
        {
            throw new ArgumentException("Market rent cannot be negative.", nameof(marketRent));
        }

        if (squareMeters < 0)
        {
            throw new ArgumentException("Square meters cannot be negative.", nameof(squareMeters));
        }

        if (bedrooms < 0 || bathrooms < 0)
        {
            throw new ArgumentException("Bedrooms and bathrooms cannot be negative.");
        }

        return new Unit
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PropertyId = propertyId,
            UnitNumber = unitNumber.Trim(),
            FloorNumber = floorNumber,
            Type = type,
            Status = UnitOperationalStatus.Active,
            MarketRent = marketRent,
            Bedrooms = bedrooms,
            Bathrooms = bathrooms,
            SquareMeters = squareMeters,
            Description = description?.Trim() ?? string.Empty,
            CreatedAt = utcNow
        };
    }

    public void UpdateDetails(
        string unitNumber,
        UnitType type,
        decimal marketRent,
        int? floorNumber,
        int bedrooms,
        int bathrooms,
        decimal squareMeters,
        string description,
        DateTime utcNow)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(unitNumber, nameof(unitNumber));

        if (marketRent < 0)
        {
            throw new ArgumentException("Market rent cannot be negative.", nameof(marketRent));
        }

        if (squareMeters < 0)
        {
            throw new ArgumentException("Square meters cannot be negative.", nameof(squareMeters));
        }

        if (bedrooms < 0 || bathrooms < 0)
        {
            throw new ArgumentException("Bedrooms and bathrooms cannot be negative.");
        }

        UnitNumber = unitNumber.Trim();
        Type = type;
        MarketRent = marketRent;
        FloorNumber = floorNumber;
        Bedrooms = bedrooms;
        Bathrooms = bathrooms;
        SquareMeters = squareMeters;
        Description = description?.Trim() ?? string.Empty;
        UpdatedAt = utcNow;
    }

    public void SetStatus(UnitOperationalStatus newStatus, DateTime utcNow)
    {
        if (!Enum.IsDefined(typeof(UnitOperationalStatus), newStatus))
        {
            throw new ArgumentException("Invalid operational status.", nameof(newStatus));
        }

        Status = newStatus;
        UpdatedAt = utcNow;
    }
}