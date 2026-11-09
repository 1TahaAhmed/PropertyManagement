using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PropertyManagement.Api.Controllers;
using PropertyManagement.Application.Abstractions.Security;
using Xunit;

namespace PropertyManagement.Api.Tests;

public class ApiControllerBaseTests
{
    private sealed class TestController : ApiControllerBase
    {
        public bool CheckOrg(ICurrentUser currentUser, out Guid orgId, out IActionResult? errorResult)
        {
            return TryGetOrganizationId(currentUser, out orgId, out errorResult);
        }
    }

    private sealed class FakeCurrentUser(Guid? organizationId) : ICurrentUser
    {
        public bool IsAuthenticated => true;
        public Guid? UserId => Guid.NewGuid();
        public Guid? OrganizationId => organizationId;
        public string? Role => "Owner";
        public string? Email => "owner@example.com";
    }

    [Fact]
    public void TryGetOrganizationId_WhenOrgIdPresent_ReturnsTrueAndSetsId()
    {
        // Arrange
        var controller = new TestController();
        var expectedOrgId = Guid.NewGuid();
        var currentUser = new FakeCurrentUser(expectedOrgId);

        // Act
        var result = controller.CheckOrg(currentUser, out var orgId, out var errorResult);

        // Assert
        Assert.True(result);
        Assert.Equal(expectedOrgId, orgId);
        Assert.Null(errorResult);
    }

    [Fact]
    public void TryGetOrganizationId_WhenOrgIdNull_ReturnsFalseAndUnauthorizedProblemDetails()
    {
        // Arrange
        var controller = new TestController();
        var currentUser = new FakeCurrentUser(null);

        // Act
        var result = controller.CheckOrg(currentUser, out var orgId, out var errorResult);

        // Assert
        Assert.False(result);
        Assert.Equal(Guid.Empty, orgId);
        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(errorResult);
        var problemDetails = Assert.IsType<ProblemDetails>(unauthorizedResult.Value);
        Assert.Equal(StatusCodes.Status401Unauthorized, problemDetails.Status);
        Assert.Equal("An active organization context is required.", problemDetails.Detail);
    }
}
