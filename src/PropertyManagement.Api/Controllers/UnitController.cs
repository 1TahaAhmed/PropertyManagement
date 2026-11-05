using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PropertyManagement.Api.Authorization;
using PropertyManagement.Api.Contracts.Units;
using PropertyManagement.Api.Extensions;
using PropertyManagement.Application.Abstractions.Security;
using PropertyManagement.Application.Common.Models;
using PropertyManagement.Application.Common.Security;
using PropertyManagement.Application.Features.Units.ChangeStatus;
using PropertyManagement.Application.Features.Units.Create;
using PropertyManagement.Application.Features.Units.GetById;
using PropertyManagement.Application.Features.Units.GetUnitsByProperty;
using PropertyManagement.Application.Features.Units.Response;
using PropertyManagement.Application.Features.Units.Update;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/v1")]
public sealed class UnitsController(
    ISender sender,
    ICurrentUser currentUser) : ControllerBase
{
    private Guid? CurrentOrgId => currentUser.OrganizationId;

    [HttpPost("properties/{propertyId:guid}/units")]
    [HasPermission(Permissions.Units.Create)]
    [ProducesResponseType(typeof(UnitResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        Guid propertyId,
        [FromBody] CreateUnitRequest request,
        CancellationToken cancellationToken)
    {
        if (!CurrentOrgId.HasValue)
        {
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Unauthorized",
                Detail = "An active organization context is required."
            });
        }

        var command = new CreateUnitCommand(
            OrganizationId: CurrentOrgId.Value,
            PropertyId: propertyId,
            UnitNumber: request.UnitNumber,
            Type: request.Type,
            MarketRent: request.MarketRent,
            FloorNumber: request.FloorNumber,
            Bedrooms: request.Bedrooms,
            Bathrooms: request.Bathrooms,
            SquareMeters: request.SquareMeters,
            Description: request.Description);

        var result = await sender.Send(command, cancellationToken);
        if (result.IsFailure)
        {
            return this.ToActionResult(result);
        }

        return StatusCode(StatusCodes.Status201Created, result.Value);
    }

    [HttpGet("units/{id:guid}")]
    [HasPermission(Permissions.Units.Read)]
    [ProducesResponseType(typeof(UnitResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(
        Guid id,
        [FromQuery] Guid? organizationId,
        CancellationToken cancellationToken)
    {
        var targetOrgId = CurrentOrgId ?? organizationId;
        if (!targetOrgId.HasValue)
        {
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Unauthorized",
                Detail = "An active organization context is required."
            });
        }

        var query = new GetUnitByIdQuery(targetOrgId.Value, id);
        var result = await sender.Send(query, cancellationToken);
        if (result.IsFailure)
        {
            return this.ToActionResult(result);
        }

        return Ok(result.Value);
    }

    [HttpGet("properties/{propertyId:guid}/units")]
    [HasPermission(Permissions.Units.Read)]
    [ProducesResponseType(typeof(PagedList<UnitResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetByProperty(
        Guid propertyId,
        [FromQuery] Guid? organizationId,
        [FromQuery] UnitOperationalStatus? status,
        [FromQuery] UnitType? type,
        [FromQuery] decimal? minRent,
        [FromQuery] decimal? maxRent,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var targetOrgId = CurrentOrgId ?? organizationId;
        if (!targetOrgId.HasValue)
        {
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Unauthorized",
                Detail = "An active organization context is required."
            });
        }

        var query = new GetUnitsByPropertyQuery(targetOrgId.Value, propertyId, status, type, minRent, maxRent, pageNumber, pageSize);
        var result = await sender.Send(query, cancellationToken);
        if (result.IsFailure)
        {
            return this.ToActionResult(result);
        }

        return Ok(result.Value);
    }

    [HttpPut("units/{id:guid}")]
    [HasPermission(Permissions.Units.Update)]
    [ProducesResponseType(typeof(UnitResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateUnitRequest request,
        CancellationToken cancellationToken)
    {
        if (!CurrentOrgId.HasValue)
        {
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Unauthorized",
                Detail = "An active organization context is required."
            });
        }

        var command = new UpdateUnitCommand(
            OrganizationId: CurrentOrgId.Value,
            UnitId: id,
            UnitNumber: request.UnitNumber,
            Type: request.Type,
            MarketRent: request.MarketRent,
            FloorNumber: request.FloorNumber,
            Bedrooms: request.Bedrooms,
            Bathrooms: request.Bathrooms,
            SquareMeters: request.SquareMeters,
            Description: request.Description,
            RowVersion: request.RowVersion!);

        var result = await sender.Send(command, cancellationToken);
        if (result.IsFailure)
        {
            return this.ToActionResult(result);
        }

        return Ok(result.Value);
    }

    [HttpPost("units/{id:guid}/status")]
    [HasPermission(Permissions.Units.ChangeStatus)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ChangeStatus(
        Guid id,
        [FromBody] ChangeUnitStatusRequest request,
        CancellationToken cancellationToken)
    {
        if (!CurrentOrgId.HasValue)
        {
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Unauthorized",
                Detail = "An active organization context is required."
            });
        }

        var command = new ChangeUnitStatusCommand(id, CurrentOrgId.Value, request.NewStatus);
        var result = await sender.Send(command, cancellationToken);
        if (result.IsFailure)
        {
            return this.ToActionResult(result);
        }

        return Ok(new { message = "Unit status updated successfully." });
    }
}
