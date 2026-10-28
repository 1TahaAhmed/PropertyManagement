using MediatR;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Application.Features.Properties.Response;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Application.Features.Properties.Update;

public sealed record UpdatePropertyCommand(
    Guid OrganizationId,
    Guid PropertyId,
    string Name,
    string Address,
    string Description,
    PropertyType Type,
    string RowVersion) : IRequest<Result<PropertyResponse>>;
