using MediatR;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Application.Features.Properties.Response;

namespace PropertyManagement.Application.Features.Properties.GetById;

public sealed record GetPropertyByIdQuery(Guid PropertyId) : IRequest<Result<PropertyResponse>>;