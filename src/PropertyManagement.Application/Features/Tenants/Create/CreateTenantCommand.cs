using MediatR;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Application.Features.Tenants.Responses;

namespace PropertyManagement.Application.Features.Tenants.Create;

public sealed record CreateTenantCommand(
    Guid OrganizationId,
    string FirstName,
    string LastName,
    string Email,
    string PhoneNumber,
    string NationalIdOrPassport = "",
    string EmergencyContactName = "",
    string EmergencyContactPhone = "",
    string Notes = "") : IRequest<Result<TenantResponse>>;
