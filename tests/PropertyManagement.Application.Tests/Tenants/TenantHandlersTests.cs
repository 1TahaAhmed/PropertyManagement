using NSubstitute;
using PropertyManagement.Application.Abstractions;
using PropertyManagement.Application.Abstractions.Tenants;
using PropertyManagement.Application.Common.Enums;
using PropertyManagement.Application.Features.Tenants.Create;
using PropertyManagement.Application.Features.Tenants.GetById;
using PropertyManagement.Application.Features.Tenants.GetTenants;
using PropertyManagement.Application.Features.Tenants.Status;
using PropertyManagement.Application.Features.Tenants.Update;
using PropertyManagement.Domain.Entities.Tenants;
using PropertyManagement.Domain.Enums;
using Xunit;

namespace PropertyManagement.Application.Tests.Tenants;

public class TenantHandlersTests
{
    private static readonly DateTime FixedNow = new(2026, 11, 13, 12, 0, 0, DateTimeKind.Utc);
    private readonly ITenantRepository _tenantRepository = Substitute.For<ITenantRepository>();
    private readonly IClock _clock = Substitute.For<IClock>();

    public TenantHandlersTests()
    {
        _clock.UtcNow.Returns(FixedNow);
    }

    [Fact]
    public async Task CreateTenant_ShouldSucceed_WhenEmailIsUnique()
    {
        var orgId = Guid.NewGuid();
        var command = new CreateTenantCommand(orgId, "Mohamed", "Ali", "M.Ali@Test.com", "01012345678");

        _tenantRepository.ExistsByEmailAsync(orgId, command.Email, null, Arg.Any<CancellationToken>())
            .Returns(false);

        var handler = new CreateTenantCommandHandler(_tenantRepository, _clock);
        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("m.ali@test.com", result.Value.Email);
        Assert.Equal(TenantStatus.Active, result.Value.Status);
        await _tenantRepository.Received(1).AddAsync(Arg.Any<Tenant>(), Arg.Any<CancellationToken>());
        await _tenantRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateTenant_ShouldReturnConflict_WhenDuplicateEmailExists()
    {
        var orgId = Guid.NewGuid();
        var command = new CreateTenantCommand(orgId, "Mohamed", "Ali", "dup@test.com", "01012345678");

        _tenantRepository.ExistsByEmailAsync(orgId, command.Email, null, Arg.Any<CancellationToken>())
            .Returns(true);

        var handler = new CreateTenantCommandHandler(_tenantRepository, _clock);
        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Conflict, result.Errors.First().Type);
        Assert.Equal("Tenant.DuplicateEmail", result.Errors.First().Code);
    }

    [Fact]
    public async Task UpdateTenant_ShouldReturnValidationError_WhenTenantIsBlacklisted()
    {
        var orgId = Guid.NewGuid();
        var tenant = Tenant.Create(orgId, "Mohamed", "Ali", "m@test.com", "01012345678", FixedNow);
        tenant.Blacklist("Unpaid rent", FixedNow);

        _tenantRepository.GetByIdAsync(tenant.Id, orgId, Arg.Any<CancellationToken>()).Returns(tenant);

        var handler = new UpdateTenantCommandHandler(_tenantRepository, _clock);
        var result = await handler.Handle(
            new UpdateTenantCommand(tenant.Id, orgId, "Mohamed", "Ali", "m@test.com", "01012345678"),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Tenant.Blacklisted", result.Errors.First().Code);
    }

    [Fact]
    public async Task UpdateTenant_ShouldReturnValidationError_WhenRowVersionIsInvalidBase64()
    {
        var orgId = Guid.NewGuid();
        var tenant = Tenant.Create(orgId, "Mohamed", "Ali", "m@test.com", "01012345678", FixedNow);

        _tenantRepository.GetByIdAsync(tenant.Id, orgId, Arg.Any<CancellationToken>()).Returns(tenant);
        _tenantRepository.ExistsByEmailAsync(orgId, "m@test.com", tenant.Id, Arg.Any<CancellationToken>()).Returns(false);

        var handler = new UpdateTenantCommandHandler(_tenantRepository, _clock);
        var result = await handler.Handle(
            new UpdateTenantCommand(tenant.Id, orgId, "Mohamed", "Ali", "m@test.com", "01012345678", RowVersion: "not-base64!!!"),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Tenant.InvalidRowVersion", result.Errors.First().Code);
    }

    [Fact]
    public async Task BlacklistAndDeactivateAndActivate_ShouldUpdateStatus()
    {
        var orgId = Guid.NewGuid();
        var tenant = Tenant.Create(orgId, "Mohamed", "Ali", "m@test.com", "01012345678", FixedNow);

        _tenantRepository.GetByIdAsync(tenant.Id, orgId, Arg.Any<CancellationToken>()).Returns(tenant);

        var deactivateHandler = new DeactivateTenantCommandHandler(_tenantRepository, _clock);
        var res1 = await deactivateHandler.Handle(new DeactivateTenantCommand(tenant.Id, orgId), CancellationToken.None);
        Assert.True(res1.IsSuccess);
        Assert.Equal(TenantStatus.Inactive, tenant.Status);

        var activateHandler = new ActivateTenantCommandHandler(_tenantRepository, _clock);
        var res2 = await activateHandler.Handle(new ActivateTenantCommand(tenant.Id, orgId), CancellationToken.None);
        Assert.True(res2.IsSuccess);
        Assert.Equal(TenantStatus.Active, tenant.Status);

        var blacklistHandler = new BlacklistTenantCommandHandler(_tenantRepository, _clock);
        var res3 = await blacklistHandler.Handle(new BlacklistTenantCommand(tenant.Id, orgId, "Damage"), CancellationToken.None);
        Assert.True(res3.IsSuccess);
        Assert.Equal(TenantStatus.Blacklisted, tenant.Status);
        Assert.Contains("Damage", tenant.Notes);
    }

    [Fact]
    public async Task GetByIdAndGetTenants_ShouldReturnExpectedResults()
    {
        var orgId = Guid.NewGuid();
        var tenant = Tenant.Create(orgId, "Mohamed", "Ali", "m@test.com", "01012345678", FixedNow);

        _tenantRepository.GetByIdAsync(tenant.Id, orgId, Arg.Any<CancellationToken>()).Returns(tenant);
        _tenantRepository.GetPagedAsync(orgId, null, null, 1, 10, Arg.Any<CancellationToken>())
            .Returns(([tenant], 1));

        var byIdHandler = new GetTenantByIdQueryHandler(_tenantRepository);
        var byIdResult = await byIdHandler.Handle(new GetTenantByIdQuery(tenant.Id, orgId), CancellationToken.None);
        Assert.True(byIdResult.IsSuccess);
        Assert.Equal(tenant.Id, byIdResult.Value.Id);

        var listHandler = new GetTenantsQueryHandler(_tenantRepository);
        var listResult = await listHandler.Handle(new GetTenantsQuery(orgId), CancellationToken.None);
        Assert.True(listResult.IsSuccess);
        Assert.Single(listResult.Value.Items);
    }
}
