using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Domain.Entities.Organizations;

public sealed class Organization
{
    private Organization() { }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Identifier { get; private set; } = string.Empty; 
    public OrganizationStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    public static Organization Create(string name, DateTime utcNow)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(name));

        var trimmedName = name.Trim();
        var slug = GenerateSlug(trimmedName);

        return new Organization
        {
            Id = Guid.NewGuid(),
            Name = trimmedName,
            Identifier = slug,
            Status = OrganizationStatus.Active,
            CreatedAt = utcNow
        };
    }

    public void UpdateName(string name, DateTime utcNow)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(name));
        Name = name.Trim();
        Identifier = GenerateSlug(Name);
        UpdatedAt = utcNow;
    }

    public void Suspend(DateTime utcNow)
    {
        if (Status == OrganizationStatus.Archived)
        {
            throw new InvalidOperationException("Cannot suspend an archived organization.");
        }

        Status = OrganizationStatus.Suspended;
        UpdatedAt = utcNow;
    }

    public void Activate(DateTime utcNow)
    {
        Status = OrganizationStatus.Active;
        UpdatedAt = utcNow;
    }

    private static string GenerateSlug(string input)
    {
        return input.ToLowerInvariant()
            .Replace(" ", "-")
            .Replace(".", "-")
            .Trim('-');
    }
}