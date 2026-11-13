using MediatR;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Application.Features.Tenants.Responses;

namespace PropertyManagement.Application.Features.Tenants.Update;

public sealed record UpdateTenantCommand(
    Guid TenantId,
    Guid OrganizationId,
    string FirstName,
    string LastName,
    string Email,
    string PhoneNumber,
    string NationalIdOrPassport = "",
    string EmergencyContactName = "",
    string EmergencyContactPhone = "",
    string Notes = "",
    string? RowVersion = null) : IRequest<Result<TenantResponse>>;
