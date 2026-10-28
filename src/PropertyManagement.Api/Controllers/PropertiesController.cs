using MediatR;
using Microsoft.AspNetCore.Mvc;
using PropertyManagement.Api.Contracts.Properties;
using PropertyManagement.Api.Extensions;
using PropertyManagement.Application.Common.Models;
using PropertyManagement.Application.Features.Properties.Archive;
using PropertyManagement.Application.Features.Properties.Create;
using PropertyManagement.Application.Features.Properties.GetById;
using PropertyManagement.Application.Features.Properties.GetProperties;
using PropertyManagement.Application.Features.Properties.Response;
using PropertyManagement.Application.Features.Properties.Update;
using PropertyManagement.Domain.Enums;

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
            OrganizationId: request.OrganizationId,
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

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(PropertyResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(
        Guid id,
        [FromQuery] Guid organizationId,
        CancellationToken cancellationToken)
    {
        var query = new GetPropertyByIdQuery(organizationId, id);
        var result = await sender.Send(query, cancellationToken);
        if (result.IsFailure)
        {
            return this.ToActionResult(result);
        }

        return Ok(result.Value);
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedList<PropertyResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetPaged(
        [FromQuery] Guid organizationId,
        [FromQuery] string? searchTerm,
        [FromQuery] PropertyStatus? status,
        [FromQuery] PropertyType? type,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var query = new GetPropertiesQuery(organizationId, searchTerm, status, type, pageNumber, pageSize);
        var result = await sender.Send(query, cancellationToken);
        if (result.IsFailure)
        {
            return this.ToActionResult(result);
        }

        return Ok(result.Value);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(PropertyResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdatePropertyRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UpdatePropertyCommand(
            OrganizationId: request.OrganizationId,
            PropertyId: id,
            Name: request.Name,
            Address: request.Address,
            Description: request.Description,
            Type: request.Type,
            RowVersion: request.RowVersion);

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
        [FromQuery] Guid organizationId,
        CancellationToken cancellationToken)
    {
        var command = new ArchivePropertyCommand(id, organizationId);
        var result = await sender.Send(command, cancellationToken);
        if (result.IsFailure)
        {
            return this.ToActionResult(result);
        }

        return Ok(new { message = "Property archived successfully." });
    }
}
