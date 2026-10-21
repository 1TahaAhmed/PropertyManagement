using MediatR;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Application.Features.Units.Response;

namespace PropertyManagement.Application.Features.Units.GetById;

public sealed record GetUnitByIdQuery(Guid UnitId) : IRequest<Result<UnitResponse>>;