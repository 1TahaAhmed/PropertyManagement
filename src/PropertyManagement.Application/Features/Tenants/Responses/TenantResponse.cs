using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Application.Features.Tenants.Responses;

public sealed record TenantResponse(
    Guid Id,
    Guid OrganizationId,
    string FirstName,
    string LastName,
    string FullName,
    string Email,
    string PhoneNumber,
    string NationalIdOrPassport,
    string EmergencyContactName,
    string EmergencyContactPhone,
    TenantStatus Status,
    string Notes,
    byte[] RowVersion,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
