using MediatR;
using Microsoft.AspNetCore.Mvc;
using PropertyManagement.Api.Contracts.Properties;
using PropertyManagement.Api.Extensions;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Application.Features.Properties;
using PropertyManagement.Application.Features.Properties.Archive;
using PropertyManagement.Application.Features.Properties.Create;
using PropertyManagement.Application.Features.Properties.Response;
using PropertyManagement.Application.Features.Properties.Update;

namespace PropertyManagement.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public sealed class PropertiesController(ISender sender) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(PropertyResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        [FromBody] CreatePropertyRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreatePropertyCommand(
            TenantId: request.TenantId,
            Name: request.Name,
            Address: request.Address,
            Type: request.Type,
            Description: request.Description);

        var result = await sender.Send(command, cancellationToken);
        if (result.IsFailure)
        {
            return this.ToActionResult(result);
        }

        return StatusCode(StatusCodes.Status201Created, result.Value);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(PropertyResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdatePropertyRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UpdatePropertyCommand(
            PropertyId: id,
            Name: request.Name,
            Address: request.Address,
            Type: request.Type,
            Description: request.Description);

        var result = await sender.Send(command, cancellationToken);
        if (result.IsFailure)
        {
            return this.ToActionResult(result);
        }

        return Ok(result.Value);
    }

    [HttpPost("{id:guid}/archive")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Archive(
        Guid id,
        CancellationToken cancellationToken)
    {
        var command = new ArchivePropertyCommand(id);
        var result = await sender.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            return this.ToActionResult(result);
        }

        return Ok(new { message = "Property archived successfully." });
    }
}