using MediatR;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.VisualBasic;
using PropertyManagement.Application.Abstractions.Properties;
using PropertyManagement.Application.Common.Models;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Application.Features.Properties.Create;
using PropertyManagement.Application.Features.Units.ChangeStatus;
using PropertyManagement.Application.Features.Units.Create;
using PropertyManagement.Application.Features.Units.Response;
using PropertyManagement.Domain.Entities.Properties;
using System.Diagnostics.Contracts;
using System.Net;
using System.Net.NetworkInformation;
using static System.Net.WebRequestMethods;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace PropertyManagement.Application.Features.Units.GetUnitsByProperty;

public sealed class GetUnitsByPropertyQueryHandler(IUnitRepository unitRepository)
    : IRequestHandler<GetUnitsByPropertyQuery, Result<PagedList<UnitResponse>>>
{

    public async Task<Result<PagedList<UnitResponse>>> Handle(GetUnitsByPropertyQuery request, CancellationToken cancellationToken)
    {
        var (items, totalCount) = await unitRepository.GetPagedByPropertyAsync(
            request.PropertyId,
            request.Status,
            request.Type,
            request.MinRent,
            request.MaxRent,
            request.PageNumber,
            request.PageSize,
            cancellationToken);

        var responseItems = items.Select(u => new UnitResponse(
            u.Id,
            u.TenantId,
            u.PropertyId,
            u.UnitNumber,
            u.FloorNumber,
            u.Type,
            u.Status,
            u.MarketRent,
            u.Bedrooms,
            u.Bathrooms,
            u.SquareMeters,
            u.Description,
            u.CreatedAt,
            u.UpdatedAt,
            Convert.ToBase64String(u.RowVersion))).ToList();

        return Result<PagedList<UnitResponse>>.Success(
            new PagedList<UnitResponse>(responseItems, request.PageNumber, request.PageSize, totalCount));
    }
}