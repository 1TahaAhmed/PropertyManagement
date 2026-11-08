using PropertyManagement.Application.Features.Residents.Responses;
using PropertyManagement.Domain.Entities.Residents;

namespace PropertyManagement.Application.Features.Residents;

public static class ResidentMappingExtensions
{
    public static ResidentResponse ToResponse(this Resident resident)
    {
        return new ResidentResponse(
            Id: resident.Id,
            OrganizationId: resident.OrganizationId,
            FirstName: resident.FirstName,
            MiddleName: resident.MiddleName,
            LastName: resident.LastName,
            PreferredName: resident.PreferredName,
            FullName: resident.FullName,
            DisplayName: resident.DisplayName,
            Email: resident.Email,
            Phone: resident.Phone,
            AlternatePhone: resident.AlternatePhone,
            Status: resident.Status,
            UserId: resident.UserId,
            EmergencyContacts: resident.EmergencyContacts.Select(c => c.ToResponse()).ToList(),
            RowVersion: resident.RowVersion,
            CreatedAtUtc: resident.CreatedAtUtc,
            UpdatedAtUtc: resident.UpdatedAtUtc);
    }

    public static ResidentSummaryResponse ToSummaryResponse(this Resident resident)
    {
        return new ResidentSummaryResponse(
            Id: resident.Id,
            OrganizationId: resident.OrganizationId,
            FirstName: resident.FirstName,
            MiddleName: resident.MiddleName,
            LastName: resident.LastName,
            PreferredName: resident.PreferredName,
            FullName: resident.FullName,
            DisplayName: resident.DisplayName,
            Email: resident.Email,
            Phone: resident.Phone,
            AlternatePhone: resident.AlternatePhone,
            Status: resident.Status,
            UserId: resident.UserId,
            EmergencyContactsCount: resident.EmergencyContacts.Count,
            CreatedAtUtc: resident.CreatedAtUtc,
            UpdatedAtUtc: resident.UpdatedAtUtc);
    }

    public static EmergencyContactResponse ToResponse(this EmergencyContact contact)
    {
        return new EmergencyContactResponse(
            Id: contact.Id,
            ResidentId: contact.ResidentId,
            Name: contact.Name,
            Relationship: contact.Relationship,
            Phone: contact.Phone,
            Email: contact.Email,
            Notes: contact.Notes,
            CreatedAtUtc: contact.CreatedAtUtc,
            UpdatedAtUtc: contact.UpdatedAtUtc);
    }
}
