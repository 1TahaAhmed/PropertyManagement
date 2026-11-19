using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using PropertyManagement.Api.Authorization;
using PropertyManagement.Application.Common.Security;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Api.Tests;

public sealed class PaymentPermissionMatrixTests
{
    private static async Task<bool> IsAllowedAsync(MembershipRole? role, string permission)
    {
        var handler = new PermissionAuthorizationHandler();
        var requirement = new PermissionRequirement(permission);
        ClaimsPrincipal user = role.HasValue
            ? new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
                    new Claim(ClaimTypes.Role, role.Value.ToString())
                ],
                "TestAuth"))
            : new ClaimsPrincipal(new ClaimsIdentity());

        var context = new AuthorizationHandlerContext([requirement], user, resource: null);
        await handler.HandleAsync(context);
        return context.HasSucceeded;
    }

    [Theory]
    [InlineData(Permissions.Payments.Read)]
    [InlineData(Permissions.Payments.Create)]
    [InlineData(Permissions.Payments.Confirm)]
    [InlineData(Permissions.Payments.Cancel)]
    [InlineData(Permissions.Payments.Allocate)]
    [InlineData(Permissions.Payments.ReverseAllocation)]
    [InlineData(Permissions.Payments.Refund)]
    [InlineData(Permissions.Payments.Reconcile)]
    [InlineData(Permissions.Ledger.Read)]
    [InlineData(Permissions.Audit.Read)]
    public async Task OwnerAndAdmin_ShouldHaveAllFinancialPermissions(string permission)
    {
        Assert.True(await IsAllowedAsync(MembershipRole.Owner, permission));
        Assert.True(await IsAllowedAsync(MembershipRole.Admin, permission));
    }

    [Theory]
    [InlineData(Permissions.Payments.Read, true)]
    [InlineData(Permissions.Payments.Create, true)]
    [InlineData(Permissions.Payments.Confirm, true)]
    [InlineData(Permissions.Payments.Cancel, true)]
    [InlineData(Permissions.Payments.Allocate, true)]
    [InlineData(Permissions.Ledger.Read, true)]
    [InlineData(Permissions.Payments.Refund, false)]
    [InlineData(Permissions.Payments.ReverseAllocation, false)]
    [InlineData(Permissions.Payments.Reconcile, false)]
    [InlineData(Permissions.Audit.Read, false)]
    public async Task PropertyManager_ShouldHaveOperationalPaymentRights_ButNotHighRiskFinancialRights(string permission, bool expected)
    {
        Assert.Equal(expected, await IsAllowedAsync(MembershipRole.PropertyManager, permission));
    }

    [Theory]
    [InlineData(Permissions.Payments.Read, true)]
    [InlineData(Permissions.Payments.Create, false)]
    [InlineData(Permissions.Payments.Confirm, false)]
    [InlineData(Permissions.Payments.Cancel, false)]
    [InlineData(Permissions.Payments.Allocate, false)]
    [InlineData(Permissions.Payments.Refund, false)]
    [InlineData(Permissions.Payments.Reconcile, false)]
    [InlineData(Permissions.Ledger.Read, false)]
    [InlineData(Permissions.Audit.Read, false)]
    public async Task Staff_ShouldOnlyHaveReadPaymentsPermission(string permission, bool expected)
    {
        Assert.Equal(expected, await IsAllowedAsync(MembershipRole.Staff, permission));
    }

    [Fact]
    public async Task UnauthenticatedUser_ShouldBeDeniedFinancialPermissions()
    {
        Assert.False(await IsAllowedAsync(null, Permissions.Payments.Read));
        Assert.False(await IsAllowedAsync(null, Permissions.Payments.Refund));
    }
}
