using MediatR;
using Microsoft.AspNetCore.Mvc;
using PropertyManagement.Api.Contracts.Authentication.EmailConfirmation;
using PropertyManagement.Api.Extensions;
using PropertyManagement.Application.Abstractions.EmailConfirmation.Verify;
using PropertyManagement.Application.Features.Authentication.EmailConfirmation.Resend;

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
        [HttpPost("resend")]
        [ProducesResponseType(
        typeof(ResendEmailConfirmationResponse),
        StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
        public async Task<IActionResult> Resend(
        [FromBody] ResendEmailConfirmationRequest request,
        CancellationToken cancellationToken)
        {
            var ipAddress =
                HttpContext.Connection.RemoteIpAddress?.ToString()
                ?? "unknown";

            var command = new ResendEmailConfirmationCommand(
                UserId: request.UserId,
                IpAddress: ipAddress);

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
