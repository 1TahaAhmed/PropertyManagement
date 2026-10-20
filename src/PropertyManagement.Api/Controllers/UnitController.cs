using MediatR;
using Microsoft.AspNetCore.Mvc;
using PropertyManagement.Api.Contracts.Units;
using PropertyManagement.Api.Extensions;
using PropertyManagement.Application.Features.Units;
using PropertyManagement.Application.Features.Units.ChangeStatus;
using PropertyManagement.Application.Features.Units.Response;

namespace PropertyManagement.Api.Controllers;

[ApiController]
[Route("api/v1")]
public sealed class UnitsController(ISender sender) : ControllerBase
{
    // POST /api/v1/properties/{propertyId}/units
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

    // PUT /api/v1/units/{id}
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
            UnitId: id,
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

        return Ok(result.Value);
    }

    // POST /api/v1/units/{id}/status
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