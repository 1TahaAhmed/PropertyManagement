using MediatR;
using Microsoft.AspNetCore.Mvc;
using ApiRegisterResponse =
    PropertyManagement.Api.Contracts.Authentication.RegisterResponse;
using PropertyManagement.Api.Extensions;
using PropertyManagement.Application.Features.Authentication.Registeration;
using PropertyManagement.Api.Contracts.Authentication;
using PropertyManagement.Api.Contracts.Authentication.PasswordReset;
using PropertyManagement.Application.Features.Authentication.PasswordReset.ForgotPassword;
using PropertyManagement.Application.Features.Authentication.PasswordReset.ResetPassword;

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

    [HttpPost("forgot-password")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ForgotPassword(
        [FromBody] ForgotPasswordRequest request,
        CancellationToken cancellationToken)
    {
        var command = new ForgotPasswordCommand(request.Email);

        var result = await sender.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            return this.ToActionResult(result);
        }

        return Ok(new
        {
            message = "If an account with that email exists and is confirmed, a password reset token has been sent."
        });
    }

    [HttpPost("reset-password")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResetPassword(
        [FromBody] ResetPasswordRequest request,
        CancellationToken cancellationToken)
    {
        var command = new ResetPasswordCommand(
            Email: request.Email,
            Token: request.Token,
            NewPassword: request.NewPassword,
            ConfirmPassword: request.ConfirmPassword);

        var result = await sender.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            return this.ToActionResult(result);
        }

        return Ok(new
        {
            message = "Password has been reset successfully. You can now log in with your new password."
        });
    }
}
