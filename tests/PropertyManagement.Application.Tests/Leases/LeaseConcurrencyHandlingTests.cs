using NSubstitute;
using PropertyManagement.Application.Abstractions;
using PropertyManagement.Application.Abstractions.Properties;
using PropertyManagement.Application.Abstractions.Repositories;
using PropertyManagement.Application.Abstractions.Residents;
using PropertyManagement.Application.Common.Enums;
using PropertyManagement.Application.Features.Leases.Approve;
using PropertyManagement.Application.Features.Leases.CreateDraft;
using PropertyManagement.Application.Features.Leases.Renew;
using PropertyManagement.Domain.Entities.Leases;
using PropertyManagement.Domain.Entities.Properties;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Application.Tests.Leases;

public sealed class LeaseConcurrencyHandlingTests
{
    [Fact]
    public async Task CreateDraft_ShouldBeginSerializableTransaction_AcquireUnitLockBeforeOverlapCheck_AndCommit()
    {
        var orgId = Guid.NewGuid();
        var utcNow = new DateTime(2026, 11, 14, 12, 0, 0, DateTimeKind.Utc);
        var property = Property.Create(orgId, "Tower", "123 St", PropertyType.Residential, utcNow);
        var unit = Unit.Create(orgId, property.Id, "1A", UnitType.Apartment, 10000m, utcNow);

        var propertyRepository = Substitute.For<IPropertyRepository>();
        propertyRepository.GetByIdAsync(property.Id, orgId, Arg.Any<CancellationToken>()).Returns(property);

        var unitRepository = Substitute.For<IUnitRepository>();
        unitRepository.GetByIdAsync(unit.Id, orgId, Arg.Any<CancellationToken>()).Returns(unit);

        var residentRepository = Substitute.For<IResidentRepository>();
        var leaseRepository = Substitute.For<ILeaseRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var transaction = Substitute.For<IDbTransaction>();
        unitOfWork.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, Arg.Any<CancellationToken>()).Returns(transaction);

        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(utcNow);

        var handler = new CreateDraftLeaseCommandHandler(
            propertyRepository,
            unitRepository,
            residentRepository,
            leaseRepository,
            unitOfWork,
            clock);

        var result = await handler.Handle(
            new CreateDraftLeaseCommand(orgId, property.Id, unit.Id, new DateOnly(2026, 12, 1), new DateOnly(2027, 11, 30), 10000m, 20000m),
            CancellationToken.None);

        Assert.True(result.IsSuccess);

        Received.InOrder(async () =>
        {
            await unitOfWork.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, Arg.Any<CancellationToken>());
            await leaseRepository.AcquireUnitLockAsync(unit.Id, orgId, Arg.Any<CancellationToken>());
            await leaseRepository.HasOverlappingLeaseAsync(unit.Id, orgId, new DateOnly(2026, 12, 1), new DateOnly(2027, 11, 30), null, Arg.Any<CancellationToken>());
            await leaseRepository.AddAsync(Arg.Any<Lease>(), Arg.Any<CancellationToken>());
            await unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>());
            await transaction.CommitAsync(Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task ApproveAndRenew_WhenOverlapping_ShouldLockUnderSerializable_AndAbortWithoutCommit()
    {
        var orgId = Guid.NewGuid();
        var utcNow = new DateTime(2026, 11, 14, 12, 0, 0, DateTimeKind.Utc);
        var lease = Lease.CreateDraft(
            orgId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateOnly(2026, 12, 1),
            new DateOnly(2027, 11, 30),
            new LeaseFinancialTerms(10000m, 20000m),
            utcNow);
        lease.AddResident(Guid.NewGuid(), LeaseResidentRole.PrimaryTenant, utcNow);
        lease.SubmitForApproval(utcNow);

        var leaseRepository = Substitute.For<ILeaseRepository>();
        leaseRepository.GetByIdAsync(lease.Id, orgId, Arg.Any<CancellationToken>()).Returns(lease);
        leaseRepository.HasOverlappingLeaseAsync(
            lease.UnitId,
            orgId,
            Arg.Any<DateOnly>(),
            Arg.Any<DateOnly>(),
            lease.Id,
            Arg.Any<CancellationToken>()).Returns(true);

        var unitOfWork = Substitute.For<IUnitOfWork>();
        var transaction = Substitute.For<IDbTransaction>();
        unitOfWork.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, Arg.Any<CancellationToken>()).Returns(transaction);

        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(utcNow);

        var approveHandler = new ApproveLeaseCommandHandler(leaseRepository, unitOfWork, clock);
        var approveResult = await approveHandler.Handle(new ApproveLeaseCommand(orgId, lease.Id), CancellationToken.None);

        Assert.True(approveResult.IsFailure);
        Assert.Contains(approveResult.Errors, e => e.Type == ErrorType.Conflict);
        await leaseRepository.Received(1).AcquireUnitLockAsync(lease.UnitId, orgId, Arg.Any<CancellationToken>());
        await unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        await transaction.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());

        // Renew path when overlapping
        lease.Approve(utcNow);
        var renewHandler = new RenewLeaseCommandHandler(leaseRepository, unitOfWork, clock);
        var renewResult = await renewHandler.Handle(new RenewLeaseCommand(orgId, lease.Id, new DateOnly(2028, 11, 30)), CancellationToken.None);

        Assert.True(renewResult.IsFailure);
        Assert.Contains(renewResult.Errors, e => e.Type == ErrorType.Conflict);
        await leaseRepository.Received(2).AcquireUnitLockAsync(lease.UnitId, orgId, Arg.Any<CancellationToken>());
        await unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        await transaction.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }
}
