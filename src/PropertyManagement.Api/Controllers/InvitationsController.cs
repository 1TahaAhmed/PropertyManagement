using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PropertyManagement.Api.Authorization;
using PropertyManagement.Api.Contracts.Invitations;
using PropertyManagement.Api.Extensions;
using PropertyManagement.Application.Abstractions.Security;
using PropertyManagement.Application.Common.Security;
using PropertyManagement.Application.Features.Invitations.Accept;
using PropertyManagement.Application.Features.Invitations.Create;
using PropertyManagement.Application.Features.Invitations.GetAll;
using PropertyManagement.Application.Features.Invitations.Responses;
using PropertyManagement.Application.Features.Invitations.Revoke;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/v1/[controller]")]
public sealed class InvitationsController(
    ISender sender,
    ICurrentUser currentUser) : ApiControllerBase
{
    [HttpPost]
    [HasPermission(Permissions.Invitations.Create)]
    [ProducesResponseType(typeof(InvitationResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        [FromBody] CreateInvitationRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetOrganizationId(currentUser, out var organizationId, out var errorResult))
        {
            return errorResult!;
        }

        var command = new CreateInvitationCommand(
            OrganizationId: organizationId,
            Email: request.Email,
            Role: request.Role);

        var result = await sender.Send(command, cancellationToken);
        if (result.IsFailure)
        {
            return this.ToActionResult(result);
        }

        return StatusCode(StatusCodes.Status201Created, result.Value);
    }

    [HttpGet]
    [HasPermission(Permissions.Invitations.Read)]
    [ProducesResponseType(typeof(IReadOnlyList<InvitationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAll(
        [FromQuery] InvitationStatus? status,
        CancellationToken cancellationToken)
    {
        if (!TryGetOrganizationId(currentUser, out var organizationId, out var errorResult))
        {
            return errorResult!;
        }

        var query = new GetInvitationsQuery(organizationId, status);
        var result = await sender.Send(query, cancellationToken);
        if (result.IsFailure)
        {
            return this.ToActionResult(result);
        }

        return Ok(result.Value);
    }

    [HttpPost("{id:guid}/revoke")]
    [HasPermission(Permissions.Invitations.Revoke)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Revoke(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (!TryGetOrganizationId(currentUser, out var organizationId, out var errorResult))
        {
            return errorResult!;
        }

        var command = new RevokeInvitationCommand(organizationId, id);
        var result = await sender.Send(command, cancellationToken);
        if (result.IsFailure)
        {
            return this.ToActionResult(result);
        }

        return Ok(new { message = "Invitation revoked successfully." });
    }

    [HttpPost("accept")]
    [ProducesResponseType(typeof(AcceptInvitationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Accept(
        [FromBody] AcceptInvitationRequest request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Unauthorized",
                Detail = "Authenticated user context is required."
            });
        }

        var command = new AcceptInvitationCommand(
            UserId: currentUser.UserId.Value,
            UserEmail: currentUser.Email,
            Token: request.Token);

        var result = await sender.Send(command, cancellationToken);
        if (result.IsFailure)
        {
            return this.ToActionResult(result);
        }

        return Ok(result.Value);
    }
}
