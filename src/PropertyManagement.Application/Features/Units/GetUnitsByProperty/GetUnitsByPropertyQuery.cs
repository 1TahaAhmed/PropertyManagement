using MediatR;
using PropertyManagement.Application.Common.Models;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Application.Features.Units.Response;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Application.Features.Units.GetUnitsByProperty;

public sealed record GetUnitsByPropertyQuery(
    Guid PropertyId,
    UnitOperationalStatus? Status = null,
    UnitType? Type = null,
    decimal? MinRent = null,
    decimal? MaxRent = null,
    int PageNumber = 1,
    int PageSize = 10) : IRequest<Result<PagedList<UnitResponse>>>;