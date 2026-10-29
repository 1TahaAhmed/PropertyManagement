using PropertyManagement.Domain.Common;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Domain.Entities.Tenants;

public sealed class Tenant : IOrganizationScoped
{
    private Tenant() { }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }

    public string FirstName { get; private set; } = string.Empty;
    public string LastName { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string PhoneNumber { get; private set; } = string.Empty;

    public string NationalIdOrPassport { get; private set; } = string.Empty;
    public string EmergencyContactName { get; private set; } = string.Empty;
    public string EmergencyContactPhone { get; private set; } = string.Empty;

    public TenantStatus Status { get; private set; }
    public string Notes { get; private set; } = string.Empty;

    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    public string FullName => $"{FirstName} {LastName}".Trim();

    public static Tenant Create(
        Guid organizationId,
        string firstName,
        string lastName,
        string email,
        string phoneNumber,
        DateTime utcNow,
        string nationalIdOrPassport = "",
        string emergencyContactName = "",
        string emergencyContactPhone = "",
        string notes = "")
    {
        if (organizationId == Guid.Empty)
        {
            throw new ArgumentException("Organization ID cannot be empty.", nameof(organizationId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(firstName, nameof(firstName));
        ArgumentException.ThrowIfNullOrWhiteSpace(lastName, nameof(lastName));
        ArgumentException.ThrowIfNullOrWhiteSpace(email, nameof(email));
        ArgumentException.ThrowIfNullOrWhiteSpace(phoneNumber, nameof(phoneNumber));

        return new Tenant
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            FirstName = firstName.Trim(),
            LastName = lastName.Trim(),
            Email = email.Trim().ToLowerInvariant(),
            PhoneNumber = phoneNumber.Trim(),
            NationalIdOrPassport = nationalIdOrPassport?.Trim() ?? string.Empty,
            EmergencyContactName = emergencyContactName?.Trim() ?? string.Empty,
            EmergencyContactPhone = emergencyContactPhone?.Trim() ?? string.Empty,
            Notes = notes?.Trim() ?? string.Empty,
            Status = TenantStatus.Active,
            CreatedAt = utcNow
        };
    }

    public void UpdateProfile(
        string firstName,
        string lastName,
        string email,
        string phoneNumber,
        string nationalIdOrPassport,
        string emergencyContactName,
        string emergencyContactPhone,
        string notes,
        DateTime utcNow)
    {
        if (Status == TenantStatus.Blacklisted)
        {
            throw new InvalidOperationException("Cannot update details of a blacklisted tenant.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(firstName, nameof(firstName));
        ArgumentException.ThrowIfNullOrWhiteSpace(lastName, nameof(lastName));
        ArgumentException.ThrowIfNullOrWhiteSpace(email, nameof(email));
        ArgumentException.ThrowIfNullOrWhiteSpace(phoneNumber, nameof(phoneNumber));

        FirstName = firstName.Trim();
        LastName = lastName.Trim();
        Email = email.Trim().ToLowerInvariant();
        PhoneNumber = phoneNumber.Trim();
        NationalIdOrPassport = nationalIdOrPassport?.Trim() ?? string.Empty;
        EmergencyContactName = emergencyContactName?.Trim() ?? string.Empty;
        EmergencyContactPhone = emergencyContactPhone?.Trim() ?? string.Empty;
        Notes = notes?.Trim() ?? string.Empty;
        UpdatedAt = utcNow;
    }

    public void Deactivate(DateTime utcNow)
    {
        if (Status == TenantStatus.Inactive)
            return;

        Status = TenantStatus.Inactive;
        UpdatedAt = utcNow;
    }

    public void Activate(DateTime utcNow)
    {
        if (Status == TenantStatus.Active)
            return;

        Status = TenantStatus.Active;
        UpdatedAt = utcNow;
    }

    public void Blacklist(string reason, DateTime utcNow)
    {
        Status = TenantStatus.Blacklisted;
        if (!string.IsNullOrWhiteSpace(reason))
        {
            Notes = string.IsNullOrWhiteSpace(Notes)
                ? $"[BLACKLISTED: {reason.Trim()}]"
                : $"{Notes}\n[BLACKLISTED: {reason.Trim()}]";
        }
        UpdatedAt = utcNow;
    }
}
