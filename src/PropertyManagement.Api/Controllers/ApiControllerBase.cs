using Microsoft.AspNetCore.Mvc;
using PropertyManagement.Application.Abstractions.Security;
using PropertyManagement.Domain.Enums;

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

    protected bool TryGetActorContext(
        ICurrentUser currentUser,
        out Guid organizationId,
        out Guid actorUserId,
        out MembershipRole actorRole,
        out IActionResult? errorResult)
    {
        if (!TryGetOrganizationId(currentUser, out organizationId, out errorResult))
        {
            actorUserId = Guid.Empty;
            actorRole = default;
            return false;
        }

        if (!currentUser.UserId.HasValue)
        {
            actorUserId = Guid.Empty;
            actorRole = default;
            errorResult = Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Unauthorized",
                Detail = "Authenticated user context is required."
            });
            return false;
        }

        if (string.IsNullOrWhiteSpace(currentUser.Role) ||
            !Enum.TryParse<MembershipRole>(currentUser.Role, ignoreCase: true, out actorRole) ||
            !Enum.IsDefined(actorRole))
        {
            actorUserId = Guid.Empty;
            actorRole = default;
            errorResult = Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Unauthorized",
                Detail = "A valid active membership role claim is required."
            });
            return false;
        }

        actorUserId = currentUser.UserId.Value;
        errorResult = null;
        return true;
    }
}
