using MediatR;
using Microsoft.AspNetCore.Mvc;
using PropertyManagement.Api.Extensions;
using PropertyManagement.Application.Features.Organizations.Register;
using PropertyManagement.Application.Features.Organizations.Responses;

namespace PropertyManagement.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public sealed class OrganizationsController(ISender sender) : ControllerBase
{
    public sealed record RegisterOrganizationRequest(
        string Name,
        Guid OwnerUserId);

    [HttpPost]
    [ProducesResponseType(typeof(OrganizationResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register(
        [FromBody] RegisterOrganizationRequest request,
        CancellationToken cancellationToken)
    {
        var command = new RegisterOrganizationCommand(
            Name: request.Name,
            OwnerUserId: request.OwnerUserId);

        var result = await sender.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            return this.ToActionResult(result);
        }

        return StatusCode(StatusCodes.Status201Created, result.Value);
    }
}