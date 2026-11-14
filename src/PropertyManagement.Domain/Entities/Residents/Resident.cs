using System.Text.RegularExpressions;
using PropertyManagement.Domain.Common;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Domain.Entities.Residents;

public sealed class Resident : IOrganizationScoped
{
    private static readonly Regex EmailRegex = new(
        @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly List<EmergencyContact> _emergencyContacts = [];

    private Resident() { }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public string FirstName { get; private set; } = string.Empty;
    public string? MiddleName { get; private set; }
    public string LastName { get; private set; } = string.Empty;
    public string? PreferredName { get; private set; }
    public string? Email { get; private set; }
    public string Phone { get; private set; } = string.Empty;
    public string? AlternatePhone { get; private set; }
    public string NationalIdOrPassport { get; private set; } = string.Empty;
    public string Notes { get; private set; } = string.Empty;
    public ResidentStatus Status { get; private set; }
    public Guid? UserId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public IReadOnlyCollection<EmergencyContact> EmergencyContacts => _emergencyContacts.AsReadOnly();

    public string FullName => $"{FirstName} {LastName}".Trim();
    public string DisplayName => !string.IsNullOrWhiteSpace(PreferredName) ? $"{PreferredName} {LastName}".Trim() : FullName;
    public bool IsEligibleForLease => Status == ResidentStatus.Active;

    public static Resident Create(
        Guid organizationId,
        string firstName,
        string lastName,
        string phone,
        DateTime utcNow,
        string? middleName = null,
        string? preferredName = null,
        string? email = null,
        string? alternatePhone = null,
        Guid? userId = null,
        string nationalIdOrPassport = "",
        string notes = "")
    {
        if (organizationId == Guid.Empty)
            throw new ArgumentException("Organization ID cannot be empty.", nameof(organizationId));

        ArgumentException.ThrowIfNullOrWhiteSpace(firstName, nameof(firstName));
        ArgumentException.ThrowIfNullOrWhiteSpace(lastName, nameof(lastName));
        ArgumentException.ThrowIfNullOrWhiteSpace(phone, nameof(phone));

        string? normalizedEmail = null;
        if (!string.IsNullOrWhiteSpace(email))
        {
            var trimmedEmail = email.Trim().ToLowerInvariant();
            if (!EmailRegex.IsMatch(trimmedEmail))
                throw new ArgumentException("Invalid email format.", nameof(email));

            normalizedEmail = trimmedEmail;
        }

        return new Resident
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            FirstName = firstName.Trim(),
            MiddleName = string.IsNullOrWhiteSpace(middleName) ? null : middleName.Trim(),
            LastName = lastName.Trim(),
            PreferredName = string.IsNullOrWhiteSpace(preferredName) ? null : preferredName.Trim(),
            Email = normalizedEmail,
            Phone = phone.Trim(),
            AlternatePhone = string.IsNullOrWhiteSpace(alternatePhone) ? null : alternatePhone.Trim(),
            NationalIdOrPassport = nationalIdOrPassport?.Trim() ?? string.Empty,
            Notes = notes?.Trim() ?? string.Empty,
            Status = ResidentStatus.Active,
            UserId = userId == Guid.Empty ? null : userId,
            CreatedAtUtc = utcNow
        };
    }

    public void UpdateDetails(
        string firstName,
        string lastName,
        string phone,
        DateTime utcNow,
        string? middleName = null,
        string? preferredName = null,
        string? email = null,
        string? alternatePhone = null,
        string nationalIdOrPassport = "",
        string notes = "")
    {
        EnsureNotArchived();
        if (Status == ResidentStatus.Blacklisted)
            throw new InvalidOperationException("Cannot update details of a blacklisted resident.");

        ArgumentException.ThrowIfNullOrWhiteSpace(firstName, nameof(firstName));
        ArgumentException.ThrowIfNullOrWhiteSpace(lastName, nameof(lastName));
        ArgumentException.ThrowIfNullOrWhiteSpace(phone, nameof(phone));

        string? normalizedEmail = null;
        if (!string.IsNullOrWhiteSpace(email))
        {
            var trimmedEmail = email.Trim().ToLowerInvariant();
            if (!EmailRegex.IsMatch(trimmedEmail))
                throw new ArgumentException("Invalid email format.", nameof(email));

            normalizedEmail = trimmedEmail;
        }

        FirstName = firstName.Trim();
        MiddleName = string.IsNullOrWhiteSpace(middleName) ? null : middleName.Trim();
        LastName = lastName.Trim();
        PreferredName = string.IsNullOrWhiteSpace(preferredName) ? null : preferredName.Trim();
        Email = normalizedEmail;
        Phone = phone.Trim();
        AlternatePhone = string.IsNullOrWhiteSpace(alternatePhone) ? null : alternatePhone.Trim();
        NationalIdOrPassport = nationalIdOrPassport?.Trim() ?? string.Empty;
        Notes = notes?.Trim() ?? string.Empty;
        UpdatedAtUtc = utcNow;
    }

    public EmergencyContact AddEmergencyContact(
        string name,
        string relationship,
        string phone,
        DateTime utcNow,
        string? email = null,
        string? notes = null)
    {
        EnsureNotArchived();

        var contact = EmergencyContact.Create(
            residentId: Id,
            organizationId: OrganizationId,
            name: name,
            relationship: relationship,
            phone: phone,
            utcNow: utcNow,
            email: email,
            notes: notes);

        _emergencyContacts.Add(contact);
        UpdatedAtUtc = utcNow;

        return contact;
    }

    public EmergencyContact UpdateEmergencyContact(
        Guid contactId,
        string name,
        string relationship,
        string phone,
        DateTime utcNow,
        string? email = null,
        string? notes = null)
    {
        EnsureNotArchived();

        var contact = _emergencyContacts.FirstOrDefault(c => c.Id == contactId)
            ?? throw new InvalidOperationException($"Emergency contact '{contactId}' was not found.");

        contact.Update(name, relationship, phone, utcNow, email, notes);
        UpdatedAtUtc = utcNow;

        return contact;
    }

    public void RemoveEmergencyContact(Guid contactId, DateTime utcNow)
    {
        EnsureNotArchived();

        var contact = _emergencyContacts.FirstOrDefault(c => c.Id == contactId)
            ?? throw new InvalidOperationException($"Emergency contact '{contactId}' was not found.");

        _emergencyContacts.Remove(contact);
        UpdatedAtUtc = utcNow;
    }

    public void Archive(DateTime utcNow)
    {
        if (Status == ResidentStatus.Archived)
            return;

        Status = ResidentStatus.Archived;
        UpdatedAtUtc = utcNow;
    }

    public void Deactivate(DateTime utcNow)
    {
        EnsureNotArchived();
        if (Status == ResidentStatus.Inactive)
            return;

        Status = ResidentStatus.Inactive;
        UpdatedAtUtc = utcNow;
    }

    public void Activate(DateTime utcNow)
    {
        EnsureNotArchived();
        if (Status == ResidentStatus.Active)
            return;

        Status = ResidentStatus.Active;
        UpdatedAtUtc = utcNow;
    }

    public void Blacklist(string reason, DateTime utcNow)
    {
        EnsureNotArchived();

        Status = ResidentStatus.Blacklisted;
        if (!string.IsNullOrWhiteSpace(reason))
        {
            Notes = string.IsNullOrWhiteSpace(Notes)
                ? $"[BLACKLISTED: {reason.Trim()}]"
                : $"{Notes}\n[BLACKLISTED: {reason.Trim()}]";
        }
        UpdatedAtUtc = utcNow;
    }

    public void LinkUser(Guid userId, DateTime utcNow)
    {
        EnsureNotArchived();
        if (userId == Guid.Empty)
            throw new ArgumentException("User ID cannot be empty.", nameof(userId));

        UserId = userId;
        UpdatedAtUtc = utcNow;
    }

    public void UnlinkUser(DateTime utcNow)
    {
        EnsureNotArchived();
        UserId = null;
        UpdatedAtUtc = utcNow;
    }

    private void EnsureNotArchived()
    {
        if (Status == ResidentStatus.Archived)
            throw new InvalidOperationException("Operation cannot be performed on an archived resident.");
    }
}
