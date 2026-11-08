using MediatR;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Application.Features.Residents.Responses;

namespace PropertyManagement.Application.Features.Residents.Create;

public sealed record CreateResidentCommand(
    Guid OrganizationId,
    string FirstName,
    string LastName,
    string Phone,
    string? MiddleName = null,
    string? PreferredName = null,
    string? Email = null,
    string? AlternatePhone = null) : IRequest<Result<ResidentResponse>>;
