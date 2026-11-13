using PropertyManagement.Application.Features.Tenants.Responses;
using PropertyManagement.Domain.Entities.Tenants;

namespace PropertyManagement.Application.Features.Tenants;

public static class TenantMappingExtensions
{
    public static TenantResponse ToResponse(this Tenant tenant) => new(
        Id: tenant.Id,
        OrganizationId: tenant.OrganizationId,
        FirstName: tenant.FirstName,
        LastName: tenant.LastName,
        FullName: tenant.FullName,
        Email: tenant.Email,
        PhoneNumber: tenant.PhoneNumber,
        NationalIdOrPassport: tenant.NationalIdOrPassport,
        EmergencyContactName: tenant.EmergencyContactName,
        EmergencyContactPhone: tenant.EmergencyContactPhone,
        Status: tenant.Status,
        Notes: tenant.Notes,
        RowVersion: tenant.RowVersion,
        CreatedAt: tenant.CreatedAt,
        UpdatedAt: tenant.UpdatedAt);
}
