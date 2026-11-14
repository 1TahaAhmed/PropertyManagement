using System.Data;
using NSubstitute;
using PropertyManagement.Application.Abstractions;
using PropertyManagement.Application.Abstractions.Properties;
using PropertyManagement.Application.Abstractions.Repositories;
using PropertyManagement.Application.Abstractions.Residents;
using PropertyManagement.Application.Common.Enums;
using PropertyManagement.Application.Features.Leases.AddResident;
using PropertyManagement.Application.Features.Leases.ChangeResidentRole;
using PropertyManagement.Application.Features.Leases.CreateDraft;
using PropertyManagement.Application.Features.Leases.RemoveResident;
using PropertyManagement.Application.Features.Leases.UpdateDraft;
using PropertyManagement.Application.Features.Residents.GetLeaseHolderById;
using PropertyManagement.Application.Features.Residents.Status;
using PropertyManagement.Application.Features.Residents.Update;
using PropertyManagement.Domain.Entities.Leases;
using PropertyManagement.Domain.Entities.Properties;
using PropertyManagement.Domain.Entities.Residents;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Application.Tests;

public sealed class ResidentUnifiedAndLeaseOperationsTests
{
    private static readonly DateTime FixedNow = new(2026, 11, 14, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task BlacklistResident_ShouldBlockUpdateAndLeaseAssignment_UntilActivated()
    {
        var orgId = Guid.NewGuid();
        var resident = Resident.Create(orgId, "Khaled", "Mansour", "+201012345678", FixedNow, nationalIdOrPassport: "29901011234567");
        var residentRepo = Substitute.For<IResidentRepository>();
        residentRepo.GetByIdAsync(resident.Id, orgId, Arg.Any<CancellationToken>()).Returns(resident);
        residentRepo.GetByIdWithContactsAsync(resident.Id, orgId, Arg.Any<CancellationToken>()).Returns(resident);
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(FixedNow);

        var blacklistHandler = new BlacklistResidentCommandHandler(residentRepo, clock);
        var blResult = await blacklistHandler.Handle(new BlacklistResidentCommand(resident.Id, orgId, "Fraud"), CancellationToken.None);
        Assert.True(blResult.IsSuccess);
        Assert.Equal(ResidentStatus.Blacklisted, resident.Status);

        var updateHandler = new UpdateResidentCommandHandler(residentRepo, clock);
        var updateResult = await updateHandler.Handle(
            new UpdateResidentCommand(resident.Id, orgId, "Khaled", "Mansour", "+201012345678"),
            CancellationToken.None);
        Assert.True(updateResult.IsFailure);
        Assert.Equal("Resident.Blacklisted", updateResult.Errors.First().Code);

        var lease = Lease.CreateDraft(orgId, Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 12, 1), new DateOnly(2027, 11, 30), new LeaseFinancialTerms(10000m, 20000m), FixedNow);
        var leaseRepo = Substitute.For<ILeaseRepository>();
        leaseRepo.GetByIdAsync(lease.Id, orgId, Arg.Any<CancellationToken>()).Returns(lease);
        var uow = Substitute.For<IUnitOfWork>();

        var addHandler = new AddResidentToLeaseCommandHandler(leaseRepo, residentRepo, uow, clock);
        var addResult = await addHandler.Handle(new AddResidentToLeaseCommand(orgId, lease.Id, resident.Id, LeaseResidentRole.PrimaryTenant), CancellationToken.None);
        Assert.True(addResult.IsFailure);
        Assert.Equal("Resident.Blacklisted", addResult.Errors.First().Code);

        var activateHandler = new ActivateResidentCommandHandler(residentRepo, clock);
        var actResult = await activateHandler.Handle(new ActivateResidentCommand(resident.Id, orgId), CancellationToken.None);
        Assert.True(actResult.IsSuccess);
        Assert.Equal(ResidentStatus.Active, resident.Status);
    }

    [Fact]
    public async Task CreateDraftLease_ShouldRejectBlacklistedPrimaryResident()
    {
        var orgId = Guid.NewGuid();
        var property = Property.Create(orgId, "Tower", "123 St", PropertyType.Residential, FixedNow);
        var unit = Unit.Create(orgId, property.Id, "A1", UnitType.Apartment, 10000m, FixedNow);
        var resident = Resident.Create(orgId, "A", "B", "+201000000000", FixedNow);
        resident.Blacklist("Late default", FixedNow);

        var propRepo = Substitute.For<IPropertyRepository>();
        propRepo.GetByIdAsync(property.Id, orgId, Arg.Any<CancellationToken>()).Returns(property);
        var unitRepo = Substitute.For<IUnitRepository>();
        unitRepo.GetByIdAsync(unit.Id, orgId, Arg.Any<CancellationToken>()).Returns(unit);
        var resRepo = Substitute.For<IResidentRepository>();
        resRepo.GetByIdAsync(resident.Id, orgId, Arg.Any<CancellationToken>()).Returns(resident);
        var leaseRepo = Substitute.For<ILeaseRepository>();
        var uow = Substitute.For<IUnitOfWork>();
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(FixedNow);

        var handler = new CreateDraftLeaseCommandHandler(propRepo, unitRepo, resRepo, leaseRepo, uow, clock);
        var result = await handler.Handle(
            new CreateDraftLeaseCommand(orgId, property.Id, unit.Id, new DateOnly(2026, 12, 1), new DateOnly(2027, 11, 30), 10000m, 20000m, PrimaryResidentId: resident.Id),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Resident.Blacklisted", result.Errors.First().Code);
    }

    [Fact]
    public async Task UpdateDraft_RemoveResident_And_ChangeResidentRole_ShouldWorkInDraftState()
    {
        var orgId = Guid.NewGuid();
        var lease = Lease.CreateDraft(orgId, Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 12, 1), new DateOnly(2027, 11, 30), new LeaseFinancialTerms(10000m, 20000m), FixedNow);
        var r1 = Guid.NewGuid();
        var r2 = Guid.NewGuid();
        lease.AddResident(r1, LeaseResidentRole.PrimaryTenant, FixedNow);
        lease.AddResident(r2, LeaseResidentRole.Occupant, FixedNow);

        var leaseRepo = Substitute.For<ILeaseRepository>();
        leaseRepo.GetByIdAsync(lease.Id, orgId, Arg.Any<CancellationToken>()).Returns(lease);
        var uow = Substitute.For<IUnitOfWork>();
        var tx = Substitute.For<PropertyManagement.Application.Abstractions.IDbTransaction>();
        uow.BeginTransactionAsync(IsolationLevel.Serializable, Arg.Any<CancellationToken>()).Returns(tx);
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(FixedNow.AddMinutes(5));

        var updateHandler = new UpdateDraftLeaseCommandHandler(leaseRepo, uow, clock);
        var updateRes = await updateHandler.Handle(
            new UpdateDraftLeaseCommand(orgId, lease.Id, new DateOnly(2026, 12, 15), new DateOnly(2027, 12, 14), 11500m, 23000m),
            CancellationToken.None);
        Assert.True(updateRes.IsSuccess);
        Assert.Equal(11500m, updateRes.Value.RentAmount);

        var roleHandler = new ChangeLeaseResidentRoleCommandHandler(leaseRepo, uow, clock);
        var roleRes = await roleHandler.Handle(
            new ChangeLeaseResidentRoleCommand(orgId, lease.Id, r2, LeaseResidentRole.CoTenant),
            CancellationToken.None);
        Assert.True(roleRes.IsSuccess);
        Assert.True(roleRes.Value.Residents.Single(x => x.ResidentId == r2).IsFinanciallyResponsible);

        var removeHandler = new RemoveResidentFromLeaseCommandHandler(leaseRepo, uow, clock);
        var remRes = await removeHandler.Handle(new RemoveResidentFromLeaseCommand(orgId, lease.Id, r2), CancellationToken.None);
        Assert.True(remRes.IsSuccess);
        Assert.Single(remRes.Value.Residents);
    }

    [Fact]
    public async Task GetLeaseHolderById_ShouldReturnNotFound_WhenResidentIsNotLeaseHolder()
    {
        var orgId = Guid.NewGuid();
        var resident = Resident.Create(orgId, "Nour", "Adel", "+201001112233", FixedNow);
        var resRepo = Substitute.For<IResidentRepository>();
        resRepo.GetByIdWithContactsAsync(resident.Id, orgId, Arg.Any<CancellationToken>()).Returns(resident);
        resRepo.IsLeaseHolderAsync(resident.Id, orgId, Arg.Any<CancellationToken>()).Returns(false);

        var handler = new GetLeaseHolderByIdQueryHandler(resRepo);
        var result = await handler.Handle(new GetLeaseHolderByIdQuery(resident.Id, orgId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.NotFound, result.Errors.First().Type);
    }

}
