using Microsoft.AspNetCore.Authorization;

namespace PropertyManagement.Api.Authorization;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
public sealed class HasPermissionAttribute(string permission) : AuthorizeAttribute(policy: permission)
{
    public string Permission => Policy!;
}
