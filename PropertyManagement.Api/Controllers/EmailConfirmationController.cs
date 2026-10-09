using MediatR;
using Microsoft.AspNetCore.Mvc;
using PropertyManagement.Api.Extensions;
using PropertyManagement.Application.Features.Authentication.EmailConfirmation.Start;

namespace PropertyManagement.Api.Controllers;

[ApiController]
[Route("api/v1/email-confirmation")]
public sealed class EmailConfirmationController(
    ISender sender) : ControllerBase
{
    [HttpGet("start")]
    [ProducesResponseType(
        typeof(StartEmailConfirmationResponse),
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Start(
        [FromQuery] Guid userId,
        [FromQuery] string token,
        CancellationToken cancellationToken)
    {
        var command = new StartEmailConfirmationCommand(
            UserId: userId,
            Token: token);

        var result = await sender.Send(
            command,
            cancellationToken);

        if (result.IsFailure)
        {
            return this.ToActionResult(result);
        }

        return Ok(result.Value);
    }
}
