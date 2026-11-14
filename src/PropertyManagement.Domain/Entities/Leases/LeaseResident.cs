using PropertyManagement.Domain.Common;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Domain.Entities.Leases;

public sealed class LeaseResident : IOrganizationScoped
{
    private LeaseResident() { }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid LeaseId { get; private set; }
    public Guid ResidentId { get; private set; }
    public LeaseResidentRole Role { get; private set; }
    public DateTime AddedAtUtc { get; private set; }

    public bool IsFinanciallyResponsible => Role is LeaseResidentRole.PrimaryTenant or LeaseResidentRole.CoTenant;
    public bool CanManageLease => Role is LeaseResidentRole.PrimaryTenant or LeaseResidentRole.CoTenant;
    public bool IsOccupant => Role != LeaseResidentRole.Guarantor;
    public bool RequiresLegalIdentity => Role is LeaseResidentRole.PrimaryTenant or LeaseResidentRole.CoTenant or LeaseResidentRole.Guarantor;

    public static LeaseResident Create(
        Guid organizationId,
        Guid leaseId,
        Guid residentId,
        LeaseResidentRole role,
        DateTime utcNow)
    {
        if (organizationId == Guid.Empty) throw new ArgumentException("Organization ID cannot be empty.", nameof(organizationId));
        if (leaseId == Guid.Empty) throw new ArgumentException("Lease ID cannot be empty.", nameof(leaseId));
        if (residentId == Guid.Empty) throw new ArgumentException("Resident ID cannot be empty.", nameof(residentId));
        if (!Enum.IsDefined(typeof(LeaseResidentRole), role))
            throw new ArgumentException("Invalid lease resident role.", nameof(role));

        return new LeaseResident
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            LeaseId = leaseId,
            ResidentId = residentId,
            Role = role,
            AddedAtUtc = utcNow
        };
    }

    public void ChangeRole(LeaseResidentRole newRole)
    {
        if (!Enum.IsDefined(typeof(LeaseResidentRole), newRole))
            throw new ArgumentException("Invalid lease resident role.", nameof(newRole));
        Role = newRole;
    }
}
