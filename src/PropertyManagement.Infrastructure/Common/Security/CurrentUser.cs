using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using PropertyManagement.Application.Abstractions.Security;

namespace PropertyManagement.Infrastructure.Common.Security;

public sealed class CurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    private ClaimsPrincipal? User => httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;

    public Guid? UserId
    {
        get
        {
            var claimValue = User?.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? User?.FindFirstValue("sub");

            return Guid.TryParse(claimValue, out var id) ? id : null;
        }
    }

    public Guid? OrganizationId
    {
        get
        {
            var claimValue = User?.FindFirstValue("organization_id")
                ?? User?.FindFirstValue("tenant_id");

            return Guid.TryParse(claimValue, out var id) ? id : null;
        }
    }

    public string? Role =>
        User?.FindFirstValue(ClaimTypes.Role) ?? User?.FindFirstValue("role");

    public string? Email =>
        User?.FindFirstValue(ClaimTypes.Email) ?? User?.FindFirstValue("email");
}
