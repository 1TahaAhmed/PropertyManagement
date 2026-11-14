using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PropertyManagement.Api.Authorization;
using PropertyManagement.Api.Extensions;
using PropertyManagement.Application.Abstractions.Security;
using PropertyManagement.Application.Common.Models;
using PropertyManagement.Application.Common.Security;
using PropertyManagement.Application.Features.Residents.GetLeaseHolderById;
using PropertyManagement.Application.Features.Residents.GetResidents;
using PropertyManagement.Application.Features.Residents.Responses;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Api.Controllers;

/// <summary>
/// Read-only view of lease-holding residents (PrimaryTenant and CoTenant roles) backed by the unified Resident entity.
/// </summary>
[Authorize]
[ApiController]
[Route("api/v1/[controller]")]
public sealed class TenantsController(
    ISender sender,
    ICurrentUser currentUser) : ApiControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.Residents.Read)]
    [ProducesResponseType(typeof(PagedList<ResidentSummaryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? searchTerm,
        [FromQuery] ResidentStatus? status,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetOrganizationId(currentUser, out var organizationId, out var errorResult))
        {
            return errorResult!;
        }

        var query = new GetResidentsQuery(
            OrganizationId: organizationId,
            SearchTerm: searchTerm,
            Status: status,
            PageNumber: pageNumber,
            PageSize: pageSize,
            OnlyLeaseHolders: true);

        var result = await sender.Send(query, cancellationToken);
        if (result.IsFailure)
        {
            return this.ToActionResult(result);
        }

        return Ok(result.Value);
    }

    [HttpGet("{id:guid}")]
    [HasPermission(Permissions.Residents.Read)]
    [ProducesResponseType(typeof(ResidentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (!TryGetOrganizationId(currentUser, out var organizationId, out var errorResult))
        {
            return errorResult!;
        }

        var result = await sender.Send(new GetLeaseHolderByIdQuery(id, organizationId), cancellationToken);
        if (result.IsFailure)
        {
            return this.ToActionResult(result);
        }

        return Ok(result.Value);
    }
}
