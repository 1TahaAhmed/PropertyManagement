using MediatR;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Application.Features.Properties;
using PropertyManagement.Application.Features.Properties.Response;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Application.Features.Properties.Create;

public sealed record CreatePropertyCommand(
    Guid OrganizationId,
    string Name,
    string Address,
    PropertyType Type,
    string Description = "") : IRequest<Result<PropertyResponse>>;
