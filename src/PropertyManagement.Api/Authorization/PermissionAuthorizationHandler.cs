using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using PropertyManagement.Application.Common.Security;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Api.Authorization;

public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            return Task.CompletedTask;
        }

        var roleClaim = context.User.FindFirstValue(ClaimTypes.Role)
            ?? context.User.FindFirstValue("role");

        if (string.IsNullOrWhiteSpace(roleClaim))
        {
            return Task.CompletedTask;
        }

        if (!Enum.TryParse<MembershipRole>(roleClaim, ignoreCase: true, out var role))
        {
            return Task.CompletedTask;
        }

        var permissions = GetPermissionsForRole(role);
        if (permissions.Contains(requirement.Permission))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }

    private static HashSet<string> GetPermissionsForRole(MembershipRole role)
    {
        return role switch
        {
            MembershipRole.Owner or MembershipRole.Admin =>
            [
                Permissions.Properties.Read,
                Permissions.Properties.Create,
                Permissions.Properties.Update,
                Permissions.Properties.Archive,
                Permissions.Units.Read,
                Permissions.Units.Create,
                Permissions.Units.Update,
                Permissions.Units.ChangeStatus,
                Permissions.Tenants.Read,
                Permissions.Tenants.Create,
                Permissions.Tenants.Update,
                Permissions.Tenants.Deactivate,
                Permissions.Tenants.Blacklist,
                Permissions.Residents.Read,
                Permissions.Residents.Create,
                Permissions.Residents.Update,
                Permissions.Residents.Archive,
                Permissions.Residents.ManageContacts
            ],
            MembershipRole.PropertyManager =>
            [
                Permissions.Properties.Read,
                Permissions.Properties.Create,
                Permissions.Properties.Update,
                Permissions.Units.Read,
                Permissions.Units.Create,
                Permissions.Units.Update,
                Permissions.Units.ChangeStatus,
                Permissions.Tenants.Read,
                Permissions.Tenants.Create,
                Permissions.Tenants.Update,
                Permissions.Tenants.Deactivate,
                Permissions.Residents.Read,
                Permissions.Residents.Create,
                Permissions.Residents.Update,
                Permissions.Residents.ManageContacts
            ],
            MembershipRole.Staff =>
            [
                Permissions.Properties.Read,
                Permissions.Units.Read,
                Permissions.Tenants.Read,
                Permissions.Residents.Read
            ],
            _ => []
        };
    }
}
