using MediatR;
using Microsoft.AspNetCore.Mvc;
using PropertyManagement.Api.Contracts.Units;
using PropertyManagement.Api.Extensions;
using PropertyManagement.Application.Common.Models;
using PropertyManagement.Application.Features.Units.ChangeStatus;
using PropertyManagement.Application.Features.Units.Create;
using PropertyManagement.Application.Features.Units.GetById;
using PropertyManagement.Application.Features.Units.GetUnitsByProperty;
using PropertyManagement.Application.Features.Units.Response;
using PropertyManagement.Application.Features.Units.Update;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Api.Controllers;

[ApiController]
[Route("api/v1")]
public sealed class UnitsController(ISender sender) : ControllerBase
{
    [HttpPost("properties/{propertyId:guid}/units")]
    [ProducesResponseType(typeof(UnitResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        Guid propertyId,
        [FromBody] CreateUnitRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreateUnitCommand(
            TenantId: request.TenantId,
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
    [ProducesResponseType(typeof(UnitResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var query = new GetUnitByIdQuery(id);
        var result = await sender.Send(query, cancellationToken);
        if (result.IsFailure)
        {
            return this.ToActionResult(result);
        }

        return Ok(result.Value);
    }

    [HttpGet("properties/{propertyId:guid}/units")]
    [ProducesResponseType(typeof(PagedList<UnitResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetByProperty(
        Guid propertyId,
        [FromQuery] UnitOperationalStatus? status,
        [FromQuery] UnitType? type,
        [FromQuery] decimal? minRent,
        [FromQuery] decimal? maxRent,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var query = new GetUnitsByPropertyQuery(propertyId, status, type, minRent, maxRent, pageNumber, pageSize);
        var result = await sender.Send(query, cancellationToken);
        if (result.IsFailure)
        {
            return this.ToActionResult(result);
        }

        return Ok(result.Value);
    }

    [HttpPut("units/{id:guid}")]
    [ProducesResponseType(typeof(UnitResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateUnitRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UpdateUnitCommand(
            TenantId: request.TenantId,
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
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ChangeStatus(
        Guid id,
        [FromBody] ChangeUnitStatusRequest request,
        CancellationToken cancellationToken)
    {
        var command = new ChangeUnitStatusCommand(id, request.NewStatus);
        var result = await sender.Send(command, cancellationToken);
        if (result.IsFailure)
        {
            return this.ToActionResult(result);
        }

        return Ok(new { message = "Unit status updated successfully." });
    }
}