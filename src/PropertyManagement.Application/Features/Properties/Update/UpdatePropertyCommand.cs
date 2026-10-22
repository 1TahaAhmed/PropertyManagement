using MediatR;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Application.Features.Properties.Response;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Application.Features.Properties.Update;

public sealed record UpdatePropertyCommand(
    Guid TenantId,
    Guid PropertyId,
    Guid excludePropertyId,
    string Name,
    string Address,
    string Description,
    PropertyType Type,
    string RowVersion) : IRequest<Result<PropertyResponse>>;