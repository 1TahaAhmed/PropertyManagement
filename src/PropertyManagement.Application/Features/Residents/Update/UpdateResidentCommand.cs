using MediatR;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Application.Features.Residents.Responses;

namespace PropertyManagement.Application.Features.Residents.Update;

public sealed record UpdateResidentCommand(
    Guid ResidentId,
    Guid OrganizationId,
    string FirstName,
    string LastName,
    string Phone,
    string? MiddleName = null,
    string? PreferredName = null,
    string? Email = null,
    string? AlternatePhone = null,
    string? RowVersion = null) : IRequest<Result<ResidentResponse>>;
