using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PropertyManagement.Api.Authorization;
using PropertyManagement.Api.Contracts.Residents;
using PropertyManagement.Api.Extensions;
using PropertyManagement.Application.Abstractions.Security;
using PropertyManagement.Application.Common.Models;
using PropertyManagement.Application.Common.Security;
using PropertyManagement.Application.Features.Residents.Archive;
using PropertyManagement.Application.Features.Residents.Create;
using PropertyManagement.Application.Features.Residents.EmergencyContacts;
using PropertyManagement.Application.Features.Residents.GetById;
using PropertyManagement.Application.Features.Residents.GetResidents;
using PropertyManagement.Application.Features.Residents.Responses;
using PropertyManagement.Application.Features.Residents.Update;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/v1/[controller]")]
public sealed class ResidentsController(
    ISender sender,
    ICurrentUser currentUser) : ControllerBase
{
    private Guid? CurrentOrgId => currentUser.OrganizationId;

    [HttpPost]
    [HasPermission(Permissions.Residents.Create)]
    [ProducesResponseType(typeof(ResidentResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create(
        [FromBody] CreateResidentRequest request,
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

        var command = new CreateResidentCommand(
            OrganizationId: CurrentOrgId.Value,
            FirstName: request.FirstName,
            LastName: request.LastName,
            Phone: request.Phone,
            MiddleName: request.MiddleName,
            PreferredName: request.PreferredName,
            Email: request.Email,
            AlternatePhone: request.AlternatePhone);

        var result = await sender.Send(command, cancellationToken);
        if (result.IsFailure)
        {
            return this.ToActionResult(result);
        }

        return StatusCode(StatusCodes.Status201Created, result.Value);
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
        if (!CurrentOrgId.HasValue)
        {
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Unauthorized",
                Detail = "An active organization context is required."
            });
        }

        var query = new GetResidentByIdQuery(id, CurrentOrgId.Value);
        var result = await sender.Send(query, cancellationToken);
        if (result.IsFailure)
        {
            return this.ToActionResult(result);
        }

        return Ok(result.Value);
    }

    [HttpGet]
    [HasPermission(Permissions.Residents.Read)]
    [ProducesResponseType(typeof(PagedList<ResidentSummaryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetPaged(
        [FromQuery] string? searchTerm,
        [FromQuery] ResidentStatus? status,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
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

        var query = new GetResidentsQuery(
            CurrentOrgId.Value,
            searchTerm,
            status,
            pageNumber,
            pageSize);

        var result = await sender.Send(query, cancellationToken);
        if (result.IsFailure)
        {
            return this.ToActionResult(result);
        }

        return Ok(result.Value);
    }

    [HttpPut("{id:guid}")]
    [HasPermission(Permissions.Residents.Update)]
    [ProducesResponseType(typeof(ResidentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateResidentRequest request,
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

        var command = new UpdateResidentCommand(
            ResidentId: id,
            OrganizationId: CurrentOrgId.Value,
            FirstName: request.FirstName,
            LastName: request.LastName,
            Phone: request.Phone,
            MiddleName: request.MiddleName,
            PreferredName: request.PreferredName,
            Email: request.Email,
            AlternatePhone: request.AlternatePhone,
            RowVersion: request.RowVersion);

        var result = await sender.Send(command, cancellationToken);
        if (result.IsFailure)
        {
            return this.ToActionResult(result);
        }

        return Ok(result.Value);
    }

    [HttpPost("{id:guid}/archive")]
    [HasPermission(Permissions.Residents.Archive)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Archive(
        Guid id,
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

        var command = new ArchiveResidentCommand(id, CurrentOrgId.Value);
        var result = await sender.Send(command, cancellationToken);
        if (result.IsFailure)
        {
            return this.ToActionResult(result);
        }

        return Ok(new { message = "Resident archived successfully." });
    }

    [HttpPost("{residentId:guid}/emergency-contacts")]
    [HasPermission(Permissions.Residents.ManageContacts)]
    [ProducesResponseType(typeof(EmergencyContactResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddEmergencyContact(
        Guid residentId,
        [FromBody] EmergencyContactRequest request,
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

        var command = new AddEmergencyContactCommand(
            ResidentId: residentId,
            OrganizationId: CurrentOrgId.Value,
            Name: request.Name,
            Relationship: request.Relationship,
            Phone: request.Phone,
            Email: request.Email,
            Notes: request.Notes);

        var result = await sender.Send(command, cancellationToken);
        if (result.IsFailure)
        {
            return this.ToActionResult(result);
        }

        return StatusCode(StatusCodes.Status201Created, result.Value);
    }

    [HttpGet("{residentId:guid}/emergency-contacts")]
    [HasPermission(Permissions.Residents.Read)]
    [ProducesResponseType(typeof(IReadOnlyList<EmergencyContactResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEmergencyContacts(
        Guid residentId,
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

        var query = new GetEmergencyContactsQuery(residentId, CurrentOrgId.Value);
        var result = await sender.Send(query, cancellationToken);
        if (result.IsFailure)
        {
            return this.ToActionResult(result);
        }

        return Ok(result.Value);
    }

    [HttpPut("{residentId:guid}/emergency-contacts/{contactId:guid}")]
    [HasPermission(Permissions.Residents.ManageContacts)]
    [ProducesResponseType(typeof(EmergencyContactResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateEmergencyContact(
        Guid residentId,
        Guid contactId,
        [FromBody] EmergencyContactRequest request,
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

        var command = new UpdateEmergencyContactCommand(
            ResidentId: residentId,
            ContactId: contactId,
            OrganizationId: CurrentOrgId.Value,
            Name: request.Name,
            Relationship: request.Relationship,
            Phone: request.Phone,
            Email: request.Email,
            Notes: request.Notes);

        var result = await sender.Send(command, cancellationToken);
        if (result.IsFailure)
        {
            return this.ToActionResult(result);
        }

        return Ok(result.Value);
    }

    [HttpDelete("{residentId:guid}/emergency-contacts/{contactId:guid}")]
    [HasPermission(Permissions.Residents.ManageContacts)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteEmergencyContact(
        Guid residentId,
        Guid contactId,
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

        var command = new DeleteEmergencyContactCommand(
            ResidentId: residentId,
            ContactId: contactId,
            OrganizationId: CurrentOrgId.Value);

        var result = await sender.Send(command, cancellationToken);
        if (result.IsFailure)
        {
            return this.ToActionResult(result);
        }

        return Ok(new { message = "Emergency contact removed successfully." });
    }
}
