using MediatR;
using Microsoft.AspNetCore.Mvc;
using PropertyManagement.Api.Extensions;
using PropertyManagement.Application.Abstractions.EmailConfirmation.Verify;

namespace PropertyManagement.Api.Controllers;

[ApiController]
[Route("api/v1/email-confirmation")]
public sealed class EmailConfirmationController(
    ISender sender) : ControllerBase
{
    [HttpPost("verify")]
    [ProducesResponseType(
    typeof(VerifyEmailConfirmationResponse),
    StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]

    public async Task<IActionResult> Verify(
    [FromBody] VerifyEmailConfirmationRequest request,
    CancellationToken cancellationToken)
    {
        var command = new VerifyEmailConfirmationCommand(
            UserId: request.UserId,
            Code: request.Code);

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
