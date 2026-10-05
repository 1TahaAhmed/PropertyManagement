using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PropertyManagement.Application.Features.Authentication.Registeration;
using System.Reflection.Metadata.Ecma335;

namespace PropertyManagement.Api.Controllers
{
    [Route("api/v1/[controller]")]
    [ApiController]
    public sealed class AuthController(ISender sender) : ControllerBase
    {
        [HttpPost("register")]
        [ProducesResponseType(
            typeof(RegisterResponse),
            StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Register(
            [FromBody] RegisterCommand command,
            CancellationToken cancellationToken)
        {
            var result = await sender.Send(command, cancellationToken);

            if (result.IsFailure)
            {
                return BadRequest(result.Errors);
            }

            return StatusCode(StatusCodes.Status201Created, result.Value);
        }
    }
}
