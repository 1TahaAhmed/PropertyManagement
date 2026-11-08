using MediatR;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Application.Features.Residents.Responses;

namespace PropertyManagement.Application.Features.Residents.GetById;

public sealed record GetResidentByIdQuery(
    Guid ResidentId,
    Guid OrganizationId) : IRequest<Result<ResidentResponse>>;
