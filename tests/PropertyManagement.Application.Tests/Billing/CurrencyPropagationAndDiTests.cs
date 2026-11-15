using System.Data;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using PropertyManagement.Application.Abstractions;
using PropertyManagement.Application.Abstractions.Billing;
using PropertyManagement.Application.Abstractions.Organizations;
using PropertyManagement.Application.Abstractions.Properties;
using PropertyManagement.Application.Abstractions.Repositories;
using PropertyManagement.Application.Abstractions.Residents;
using PropertyManagement.Application.Common;
using PropertyManagement.Application.Features.Billing.Preview;
using PropertyManagement.Application.Features.Leases.CreateDraft;
using PropertyManagement.Application.Features.Leases.Renew;
using PropertyManagement.Application.Features.Leases.UpdateDraft;
using PropertyManagement.Application.Features.Organizations.Create;
using PropertyManagement.Application.Features.Organizations.Update;
using PropertyManagement.Domain.Entities.Billing.Services;
using PropertyManagement.Domain.Entities.Leases;
using PropertyManagement.Domain.Entities.Organizations;
using PropertyManagement.Domain.Entities.Properties;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Application.Tests.Billing;

public sealed class CurrencyPropagationAndDiTests
{
    private static readonly DateTime FixedNow = new(2026, 11, 15, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void AddApplication_ShouldRegisterBillingEngine_AndBillingHandlers_AndValidators()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();

        services.AddScoped(_ => Substitute.For<ILeaseRepository>());
        services.AddScoped(_ => Substitute.For<IChargeRepository>());
        services.AddScoped(_ => Substitute.For<IInvoiceRepository>());
        services.AddScoped(_ => Substitute.For<IInvoiceNumberGenerator>());
        services.AddScoped(_ => Substitute.For<IUnitOfWork>());
        services.AddScoped(_ => Substitute.For<IClock>());
        services.AddScoped(_ => Substitute.For<IOrganizationRepository>());
        services.AddScoped(_ => Substitute.For<IPropertyRepository>());
        services.AddScoped(_ => Substitute.For<IUnitRepository>());
        services.AddScoped(_ => Substitute.For<IResidentRepository>());

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var engine = scope.ServiceProvider.GetService<IBillingCalculationEngine>();
        var sender = scope.ServiceProvider.GetService<ISender>();
        var previewHandler = scope.ServiceProvider.GetService<IRequestHandler<PreviewBillingQuery, PropertyManagement.Application.Common.Results.Result<PropertyManagement.Application.Features.Billing.Responses.BillingPreviewResponse>>>();

        Assert.NotNull(engine);
        Assert.IsType<BillingCalculationEngine>(engine);
        Assert.NotNull(sender);
        Assert.NotNull(previewHandler);
    }

    [Fact]
    public async Task CreateAndUpdateOrganization_ShouldPersistAndReturnDefaultCurrency()
    {
        var orgRepo = Substitute.For<IOrganizationRepository>();
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(FixedNow);

        var createHandler = new CreateOrganizationCommandHandler(orgRepo, clock);
        var createRes = await createHandler.Handle(
            new CreateOrganizationCommand(Guid.NewGuid(), "Cairo Homes", DefaultCurrency: "egp"),
            CancellationToken.None);

        Assert.True(createRes.IsSuccess);
        Assert.Equal("EGP", createRes.Value.DefaultCurrency);

        var createdOrg = Organization.Create("Cairo Homes", FixedNow, defaultCurrency: "EGP");
        orgRepo.GetByIdAsync(createdOrg.Id, Arg.Any<CancellationToken>()).Returns(createdOrg);

        var updateHandler = new UpdateOrganizationCommandHandler(orgRepo, clock);
        var updateRes = await updateHandler.Handle(
            new UpdateOrganizationCommand(createdOrg.Id, "Cairo Homes Plus", DefaultCurrency: "eur"),
            CancellationToken.None);

        Assert.True(updateRes.IsSuccess);
        Assert.Equal("EUR", updateRes.Value.DefaultCurrency);
        Assert.Equal("EUR", createdOrg.DefaultCurrency);
    }

    [Fact]
    public async Task CreateDraftLease_ShouldInheritOrganizationDefaultCurrency_WhenOmitted_AndAllowOverride()
    {
        var org = Organization.Create("Nile Properties", FixedNow, defaultCurrency: "EGP");
        var property = Property.Create(org.Id, "Zamalek Tower", "1 Nile St", PropertyType.Residential, FixedNow);
        var unit = PropertyManagement.Domain.Entities.Properties.Unit.Create(org.Id, property.Id, "101", UnitType.Apartment, 15000m, FixedNow);

        var propRepo = Substitute.For<IPropertyRepository>();
        propRepo.GetByIdAsync(property.Id, org.Id, Arg.Any<CancellationToken>()).Returns(property);
        var unitRepo = Substitute.For<IUnitRepository>();
        unitRepo.GetByIdAsync(unit.Id, org.Id, Arg.Any<CancellationToken>()).Returns(unit);
        var resRepo = Substitute.For<IResidentRepository>();
        var leaseRepo = Substitute.For<ILeaseRepository>();
        var orgRepo = Substitute.For<IOrganizationRepository>();
        orgRepo.GetByIdAsync(org.Id, Arg.Any<CancellationToken>()).Returns(org);
        var uow = Substitute.For<IUnitOfWork>();
        var tx = Substitute.For<PropertyManagement.Application.Abstractions.IDbTransaction>();
        uow.BeginTransactionAsync(IsolationLevel.Serializable, Arg.Any<CancellationToken>()).Returns(tx);
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(FixedNow);

        var handler = new CreateDraftLeaseCommandHandler(propRepo, unitRepo, resRepo, leaseRepo, uow, clock, orgRepo);

        var inheritedResult = await handler.Handle(
            new CreateDraftLeaseCommand(org.Id, property.Id, unit.Id, new DateOnly(2026, 12, 1), new DateOnly(2027, 11, 30), 15000m, 30000m),
            CancellationToken.None);

        Assert.True(inheritedResult.IsSuccess);
        Assert.Equal("EGP", inheritedResult.Value.Currency);

        var explicitResult = await handler.Handle(
            new CreateDraftLeaseCommand(org.Id, property.Id, unit.Id, new DateOnly(2026, 12, 1), new DateOnly(2027, 11, 30), 1500m, 3000m, Currency: "usd"),
            CancellationToken.None);

        Assert.True(explicitResult.IsSuccess);
        Assert.Equal("USD", explicitResult.Value.Currency);
    }

    [Fact]
    public async Task UpdateDraftAndRenewLease_ShouldPreserveCurrency_AndSupportCurrencyOverride()
    {
        var orgId = Guid.NewGuid();
        var lease = Lease.CreateDraft(
            orgId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 12, 31),
            new LeaseFinancialTerms(12000m, 24000m, currency: "EGP"),
            FixedNow);
        lease.AddResident(Guid.NewGuid(), LeaseResidentRole.PrimaryTenant, FixedNow);

        var leaseRepo = Substitute.For<ILeaseRepository>();
        leaseRepo.GetByIdAsync(lease.Id, orgId, Arg.Any<CancellationToken>()).Returns(lease);
        var uow = Substitute.For<IUnitOfWork>();
        var tx = Substitute.For<PropertyManagement.Application.Abstractions.IDbTransaction>();
        uow.BeginTransactionAsync(IsolationLevel.Serializable, Arg.Any<CancellationToken>()).Returns(tx);
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(FixedNow);

        // UpdateDraft without Currency keeps EGP
        var updateHandler = new UpdateDraftLeaseCommandHandler(leaseRepo, uow, clock);
        var updateRes = await updateHandler.Handle(
            new UpdateDraftLeaseCommand(orgId, lease.Id, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), 13000m, 26000m),
            CancellationToken.None);
        Assert.True(updateRes.IsSuccess);
        Assert.Equal("EGP", updateRes.Value.Currency);

        // Activate then Renew with NewRentAmount ONLY -> must preserve EGP (previously regressed to USD!)
        lease.SubmitForApproval(FixedNow);
        lease.Approve(FixedNow);

        var renewHandler = new RenewLeaseCommandHandler(leaseRepo, uow, clock);
        var renewPreserveRes = await renewHandler.Handle(
            new RenewLeaseCommand(orgId, lease.Id, new DateOnly(2027, 12, 31), NewRentAmount: 14500m),
            CancellationToken.None);

        Assert.True(renewPreserveRes.IsSuccess);
        Assert.Equal(14500m, renewPreserveRes.Value.RentAmount);
        Assert.Equal("EGP", renewPreserveRes.Value.Currency);

        var renewOverrideRes = await renewHandler.Handle(
            new RenewLeaseCommand(orgId, lease.Id, new DateOnly(2027, 12, 31), NewCurrency: "sar"),
            CancellationToken.None);

        Assert.True(renewOverrideRes.IsSuccess);
        Assert.Equal("SAR", renewOverrideRes.Value.Currency);
    }
}
