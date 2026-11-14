using FluentValidation.TestHelper;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using PropertyManagement.Application.Abstractions.Repositories;
using PropertyManagement.Application.Abstractions.Residents;
using PropertyManagement.Application.Abstractions.Tenants;
using PropertyManagement.Application.Common;
using PropertyManagement.Application.Common.Enums;
using PropertyManagement.Application.Features.Leases.GetPaged;
using PropertyManagement.Application.Features.Residents.GetResidents;
using PropertyManagement.Application.Features.Tenants.GetTenants;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Application.Tests;

public sealed class PagingQueryValidatorsTests
{
    private readonly GetResidentsQueryValidator _residentsValidator = new();
    private readonly GetTenantsQueryValidator _tenantsValidator = new();
    private readonly GetLeasesPagedQueryValidator _leasesValidator = new();

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-50)]
    public void PagingValidators_ShouldFail_WhenPageNumberIsZeroOrNegative(int invalidPage)
    {
        var orgId = Guid.NewGuid();

        _residentsValidator.TestValidate(new GetResidentsQuery(orgId, PageNumber: invalidPage, PageSize: 10))
            .ShouldHaveValidationErrorFor(x => x.PageNumber);

        _tenantsValidator.TestValidate(new GetTenantsQuery(orgId, PageNumber: invalidPage, PageSize: 10))
            .ShouldHaveValidationErrorFor(x => x.PageNumber);

        _leasesValidator.TestValidate(new GetLeasesPagedQuery(orgId, PageNumber: invalidPage, PageSize: 20))
            .ShouldHaveValidationErrorFor(x => x.PageNumber);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    [InlineData(101)]
    [InlineData(100000)]
    public void PagingValidators_ShouldFail_WhenPageSizeIsOutOfRange(int invalidPageSize)
    {
        var orgId = Guid.NewGuid();

        _residentsValidator.TestValidate(new GetResidentsQuery(orgId, PageNumber: 1, PageSize: invalidPageSize))
            .ShouldHaveValidationErrorFor(x => x.PageSize);

        _tenantsValidator.TestValidate(new GetTenantsQuery(orgId, PageNumber: 1, PageSize: invalidPageSize))
            .ShouldHaveValidationErrorFor(x => x.PageSize);

        _leasesValidator.TestValidate(new GetLeasesPagedQuery(orgId, PageNumber: 1, PageSize: invalidPageSize))
            .ShouldHaveValidationErrorFor(x => x.PageSize);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(1, 20)]
    [InlineData(5, 100)]
    public void PagingValidators_ShouldPass_WhenPaginationIsValid(int pageNumber, int pageSize)
    {
        var orgId = Guid.NewGuid();

        _residentsValidator.TestValidate(new GetResidentsQuery(orgId, PageNumber: pageNumber, PageSize: pageSize))
            .ShouldNotHaveAnyValidationErrors();

        _tenantsValidator.TestValidate(new GetTenantsQuery(orgId, PageNumber: pageNumber, PageSize: pageSize))
            .ShouldNotHaveAnyValidationErrors();

        _leasesValidator.TestValidate(new GetLeasesPagedQuery(orgId, PageNumber: pageNumber, PageSize: pageSize))
            .ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void PagingValidators_ShouldFail_WhenStatusEnumIsInvalidOrOrganizationIdIsEmpty()
    {
        var invalidResident = _residentsValidator.TestValidate(new GetResidentsQuery(Guid.Empty, Status: (ResidentStatus)99));
        invalidResident.ShouldHaveValidationErrorFor(x => x.OrganizationId);
        invalidResident.ShouldHaveValidationErrorFor(x => x.Status);

        var invalidTenant = _tenantsValidator.TestValidate(new GetTenantsQuery(Guid.Empty, Status: (TenantStatus)99));
        invalidTenant.ShouldHaveValidationErrorFor(x => x.OrganizationId);
        invalidTenant.ShouldHaveValidationErrorFor(x => x.Status);
    }

    [Fact]
    public async Task MediatRPipeline_WhenPagingIsInvalid_ShouldReturnValidationFailureWithoutCallingRepositories()
    {
        var leaseRepository = Substitute.For<ILeaseRepository>();
        var residentRepository = Substitute.For<IResidentRepository>();
        var tenantRepository = Substitute.For<ITenantRepository>();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();
        services.AddSingleton(leaseRepository);
        services.AddSingleton(residentRepository);
        services.AddSingleton(tenantRepository);

        await using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();
        var orgId = Guid.NewGuid();

        var leasesResult = await sender.Send(new GetLeasesPagedQuery(orgId, PageNumber: 0, PageSize: 100000));
        Assert.True(leasesResult.IsFailure);
        Assert.Contains(leasesResult.Errors, e => e.Type == ErrorType.Validation && e.PropertyName == "PageNumber");
        Assert.Contains(leasesResult.Errors, e => e.Type == ErrorType.Validation && e.PropertyName == "PageSize");

        var residentsResult = await sender.Send(new GetResidentsQuery(orgId, PageNumber: 0, PageSize: 100000));
        Assert.True(residentsResult.IsFailure);
        Assert.Contains(residentsResult.Errors, e => e.Type == ErrorType.Validation && e.PropertyName == "PageNumber");
        Assert.Contains(residentsResult.Errors, e => e.Type == ErrorType.Validation && e.PropertyName == "PageSize");

        var tenantsResult = await sender.Send(new GetTenantsQuery(orgId, PageNumber: 0, PageSize: 100000));
        Assert.True(tenantsResult.IsFailure);
        Assert.Contains(tenantsResult.Errors, e => e.Type == ErrorType.Validation && e.PropertyName == "PageNumber");
        Assert.Contains(tenantsResult.Errors, e => e.Type == ErrorType.Validation && e.PropertyName == "PageSize");

        await leaseRepository.DidNotReceive().GetPagedAsync(
            Arg.Any<Guid>(), Arg.Any<Guid?>(), Arg.Any<Guid?>(), Arg.Any<LeaseStatus?>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
        await residentRepository.DidNotReceive().GetPagedAsync(
            Arg.Any<Guid>(), Arg.Any<string?>(), Arg.Any<ResidentStatus?>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
        await tenantRepository.DidNotReceive().GetPagedAsync(
            Arg.Any<Guid>(), Arg.Any<string?>(), Arg.Any<TenantStatus?>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }
}
