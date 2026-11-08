using System.Text.RegularExpressions;
using PropertyManagement.Domain.Common;

namespace PropertyManagement.Domain.Entities.Residents;

public sealed partial class EmergencyContact : IOrganizationScoped
{
    private static readonly Regex EmailRegex = new(
        @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private EmergencyContact() { }

    public Guid Id { get; private set; }
    public Guid ResidentId { get; private set; }
    public Guid OrganizationId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Relationship { get; private set; } = string.Empty;
    public string Phone { get; private set; } = string.Empty;
    public string? Email { get; private set; }
    public string? Notes { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }

    internal static EmergencyContact Create(
        Guid residentId,
        Guid organizationId,
        string name,
        string relationship,
        string phone,
        DateTime utcNow,
        string? email = null,
        string? notes = null)
    {
        if (residentId == Guid.Empty)
            throw new ArgumentException("Resident ID cannot be empty.", nameof(residentId));

        if (organizationId == Guid.Empty)
            throw new ArgumentException("Organization ID cannot be empty.", nameof(organizationId));

        ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(name));
        ArgumentException.ThrowIfNullOrWhiteSpace(relationship, nameof(relationship));
        ArgumentException.ThrowIfNullOrWhiteSpace(phone, nameof(phone));

        string? normalizedEmail = null;
        if (!string.IsNullOrWhiteSpace(email))
        {
            var trimmedEmail = email.Trim().ToLowerInvariant();
            if (!EmailRegex.IsMatch(trimmedEmail))
                throw new ArgumentException("Invalid email format.", nameof(email));
            normalizedEmail = trimmedEmail;
        }

        return new EmergencyContact
        {
            Id = Guid.NewGuid(),
            ResidentId = residentId,
            OrganizationId = organizationId,
            Name = name.Trim(),
            Relationship = relationship.Trim(),
            Phone = phone.Trim(),
            Email = normalizedEmail,
            Notes = notes?.Trim(),
            CreatedAtUtc = utcNow
        };
    }

    internal void Update(
        string name,
        string relationship,
        string phone,
        DateTime utcNow,
        string? email = null,
        string? notes = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(name));
        ArgumentException.ThrowIfNullOrWhiteSpace(relationship, nameof(relationship));
        ArgumentException.ThrowIfNullOrWhiteSpace(phone, nameof(phone));

        string? normalizedEmail = null;
        if (!string.IsNullOrWhiteSpace(email))
        {
            var trimmedEmail = email.Trim().ToLowerInvariant();
            if (!EmailRegex.IsMatch(trimmedEmail))
                throw new ArgumentException("Invalid email format.", nameof(email));
            normalizedEmail = trimmedEmail;
        }

        Name = name.Trim();
        Relationship = relationship.Trim();
        Phone = phone.Trim();
        Email = normalizedEmail;
        Notes = notes?.Trim();
        UpdatedAtUtc = utcNow;
    }
}
