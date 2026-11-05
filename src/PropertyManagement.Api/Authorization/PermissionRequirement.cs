using Microsoft.AspNetCore.Authorization;

namespace PropertyManagement.Api.Authorization;

public sealed class PermissionRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; } = permission;
}
