using Microsoft.AspNetCore.Mvc;
using PropertyManagement.Application.Abstractions.Security;

namespace PropertyManagement.Api.Controllers;

[ApiController]
public abstract class ApiControllerBase : ControllerBase
{
    protected bool TryGetOrganizationId(ICurrentUser currentUser, out Guid organizationId, out IActionResult? errorResult)
    {
        if (currentUser.OrganizationId.HasValue)
        {
            organizationId = currentUser.OrganizationId.Value;
            errorResult = null;
            return true;
        }

        organizationId = Guid.Empty;
        errorResult = Unauthorized(new ProblemDetails
        {
            Status = StatusCodes.Status401Unauthorized,
            Title = "Unauthorized",
            Detail = "An active organization context is required."
        });
        return false;
    }
}
