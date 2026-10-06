using MediatR;
using Microsoft.AspNetCore.Mvc;
using PropertyManagement.Api.Contracts.Authentication;
using PropertyManagement.Api.Extensions;
using PropertyManagement.Application.Features.Authentication.Registeration;

namespace PropertyManagement.Api.Controllers;

[Route("api/v1/[controller]")]
[ApiController]
public sealed class AuthController(ISender sender) : ControllerBase
{
    [HttpPost("register")]
    [ProducesResponseType(
        typeof(RegisterResponse),
        StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register(
        [FromBody] RegisterRequest request,
        CancellationToken cancellationToken)
    {
        var command = new RegisterCommand(
            FirstName: request.FirstName,
            LastName: request.LastName,
            Address: request.Address,
            Email: request.Email,
            Password: request.Password,
            ConfirmPassword: request.ConfirmPassword);

        var result = await sender.Send(
            command,
            cancellationToken);

        if (result.IsFailure)
        {
            return this.ToActionResult(result);
        }

        return StatusCode(
            StatusCodes.Status201Created,
            result.Value);
    }
}
