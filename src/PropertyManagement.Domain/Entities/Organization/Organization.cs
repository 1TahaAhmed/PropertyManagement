using System.Text.RegularExpressions;
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

    public static Organization Create(string name, DateTime utcNow, string? customSlug = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(name));

        var trimmedName = name.Trim();
        var slug = !string.IsNullOrWhiteSpace(customSlug)
            ? customSlug.Trim()
            : GenerateSlug(trimmedName);

        if (string.IsNullOrWhiteSpace(slug))
        {
            throw new InvalidOperationException("Failed to generate a valid organization identifier.");
        }

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
        UpdatedAt = utcNow;
    }

    public void Suspend(DateTime utcNow)
    {
        if (Status == OrganizationStatus.Archived)
            throw new InvalidOperationException("Cannot suspend an archived organization.");

        Status = OrganizationStatus.Suspended;
        UpdatedAt = utcNow;
    }

    public void Activate(DateTime utcNow)
    {
        Status = OrganizationStatus.Active;
        UpdatedAt = utcNow;
    }

    public static string GenerateSlug(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return $"org-{Guid.NewGuid():N}"[..12];

        var slug = input.Trim().ToLowerInvariant();

        slug = Regex.Replace(slug, @"[^a-z0-9\s-]", "");
        slug = Regex.Replace(slug, @"\s+", "-");
        slug = Regex.Replace(slug, @"-+", "-").Trim('-');

        if (string.IsNullOrWhiteSpace(slug) || slug.Length < 2)
        {
            return $"org-{Guid.NewGuid():N}"[..12];
        }

        return slug;
    }
}