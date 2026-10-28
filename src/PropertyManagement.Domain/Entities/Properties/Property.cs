using PropertyManagement.Domain.Common;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Domain.Entities.Properties;

public sealed class Property : ITenantScoped
{
    private Property() { }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Address { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public PropertyType Type { get; private set; }
    public PropertyStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private readonly List<Unit> _units = [];
    public IReadOnlyCollection<Unit> Units => _units.AsReadOnly();

    public byte[] RowVersion { get; private set; } = [];

    public static Property Create(
        Guid tenantId,
        string name,
        string address,
        PropertyType type,
        DateTime utcNow,
        string description = "")
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("Tenant ID cannot be empty.", nameof(tenantId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(name));
        ArgumentException.ThrowIfNullOrWhiteSpace(address, nameof(address));

        return new Property
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = name.Trim(),
            Address = address.Trim(),
            Type = type,
            Description = description?.Trim() ?? string.Empty,
            Status = PropertyStatus.Active,
            CreatedAt = utcNow
        };
    }

    public void UpdateDetails(string name, string address, string description, PropertyType type, DateTime utcNow)
    {
        if (Status == PropertyStatus.Archived)
        {
            throw new InvalidOperationException("Cannot update an archived property.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(name));
        ArgumentException.ThrowIfNullOrWhiteSpace(address, nameof(address));

        Name = name.Trim();
        Address = address.Trim();
        Description = description?.Trim() ?? string.Empty;
        Type = type;
        UpdatedAt = utcNow;
    }

    public void Archive(DateTime utcNow)
    {
        if (Status == PropertyStatus.Archived)
        {
            return;
        }

        Status = PropertyStatus.Archived;
        UpdatedAt = utcNow;
    }

    public void Activate(DateTime utcNow)
    {
        if (Status == PropertyStatus.Active)
        {
            return;
        }

        Status = PropertyStatus.Active;
        UpdatedAt = utcNow;
    }
}