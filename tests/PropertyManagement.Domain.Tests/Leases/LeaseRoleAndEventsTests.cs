using PropertyManagement.Domain.Entities.Leases;
using PropertyManagement.Domain.Entities.Leases.Events;
using PropertyManagement.Domain.Entities.Residents;
using PropertyManagement.Domain.Enums;
using Xunit;

namespace PropertyManagement.Domain.Tests.Leases;

public sealed class LeaseRoleAndEventsTests
{
    private static readonly DateTime Now = new(2026, 11, 14, 12, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(LeaseResidentRole.PrimaryTenant, true, true, true, true)]
    [InlineData(LeaseResidentRole.CoTenant, true, true, true, true)]
    [InlineData(LeaseResidentRole.Occupant, false, false, true, false)]
    [InlineData(LeaseResidentRole.Guarantor, false, false, false, true)]
    public void LeaseResident_ShouldExposeCorrectOperationCapabilitiesByRole(
        LeaseResidentRole role,
        bool expectedFinancial,
        bool expectedCanManage,
        bool expectedOccupant,
        bool expectedLegalIdentity)
    {
        var lr = LeaseResident.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), role, Now);

        Assert.Equal(expectedFinancial, lr.IsFinanciallyResponsible);
        Assert.Equal(expectedCanManage, lr.CanManageLease);
        Assert.Equal(expectedOccupant, lr.IsOccupant);
        Assert.Equal(expectedLegalIdentity, lr.RequiresLegalIdentity);
    }

    [Fact]
    public void Resident_Blacklist_ShouldSetStatus_AppendNotes_AndBlockUpdatesUntilReactivated()
    {
        var resident = Resident.Create(
            Guid.NewGuid(),
            "Tarek",
            "Samir",
            "+201001112233",
            Now,
            nationalIdOrPassport: "29805051234567");

        Assert.True(resident.IsEligibleForLease);

        resident.Blacklist("Unpaid rent default", Now.AddDays(1));
        Assert.Equal(ResidentStatus.Blacklisted, resident.Status);
        Assert.False(resident.IsEligibleForLease);
        Assert.Contains("[BLACKLISTED: Unpaid rent default]", resident.Notes);

        Assert.Throws<InvalidOperationException>(() =>
            resident.UpdateDetails("Tarek", "Samir", "+201001112233", Now.AddDays(2)));

        resident.Activate(Now.AddDays(3));
        Assert.Equal(ResidentStatus.Active, resident.Status);
        Assert.True(resident.IsEligibleForLease);
    }

    [Fact]
    public void Lease_ShouldRaiseDomainEvents_AcrossLifecycleTransitions()
    {
        var orgId = Guid.NewGuid();
        var lease = Lease.CreateDraft(
            orgId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 12, 31),
            new LeaseFinancialTerms(10000m, 20000m),
            Now);

        Assert.Contains(lease.DomainEvents, e => e is LeaseCreatedEvent);
        lease.ClearDomainEvents();

        var r1 = Guid.NewGuid();
        var r2 = Guid.NewGuid();
        lease.AddResident(r1, LeaseResidentRole.Occupant, Now);
        lease.AddResident(r2, LeaseResidentRole.PrimaryTenant, Now);

        // Cannot have two PrimaryTenants via ChangeResidentRole
        Assert.Throws<InvalidOperationException>(() =>
            lease.ChangeResidentRole(r1, LeaseResidentRole.PrimaryTenant, Now));

        lease.ChangeResidentRole(r1, LeaseResidentRole.CoTenant, Now);
        Assert.Equal(LeaseResidentRole.CoTenant, lease.Residents.Single(x => x.ResidentId == r1).Role);

        lease.SubmitForApproval(Now);
        Assert.Contains(lease.DomainEvents, e => e is LeaseSubmittedEvent);

        lease.Approve(Now);
        Assert.Contains(lease.DomainEvents, e => e is LeaseApprovedEvent);

        var renewal = lease.CreateRenewal(new DateOnly(2027, 12, 31), null, Now);
        Assert.Contains(lease.DomainEvents, e => e is LeaseRenewedEvent ev && ev.NewLeaseId == renewal.Id);
        Assert.Contains(renewal.DomainEvents, e => e is LeaseCreatedEvent);

        lease.Terminate(new DateOnly(2026, 6, 1), "Early exit", Guid.NewGuid(), Now);
        Assert.Contains(lease.DomainEvents, e => e is LeaseTerminatedEvent);
    }
}
