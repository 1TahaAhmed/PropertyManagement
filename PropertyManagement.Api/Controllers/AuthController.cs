using MediatR;
using Microsoft.AspNetCore.Mvc;
using ApiRegisterResponse =
    PropertyManagement.Api.Contracts.Authentication.RegisterResponse;
using PropertyManagement.Api.Extensions;
using PropertyManagement.Application.Features.Authentication.Registeration;
using PropertyManagement.Api.Contracts.Authentication;


namespace PropertyManagement.Api.Controllers;

[Route("api/v1/[controller]")]
[ApiController]
public sealed class AuthController(ISender sender) : ControllerBase
{
    [HttpPost("register")]
    [ProducesResponseType(
        typeof(ApiRegisterResponse),
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

        var response = new ApiRegisterResponse(
            UserId: result.Value.UserId,
            Email: result.Value.Email,
            FirstName: result.Value.FirstName,
            LastName: result.Value.LastName,
            EmailConfirmed: result.Value.EmailConfirmed,
            CreatedAt: result.Value.CreatedAt);

        return StatusCode(
            StatusCodes.Status201Created,
            response);
    }
}
