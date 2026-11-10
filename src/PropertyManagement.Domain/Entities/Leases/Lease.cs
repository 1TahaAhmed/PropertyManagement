using PropertyManagement.Domain.Common;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Domain.Entities.Leases;

public sealed class Lease : IOrganizationScoped
{
    private readonly List<LeaseResident> _residents = [];

    private Lease() { }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid UnitId { get; private set; }
    public DateOnly StartDate { get; private set; }
    public DateOnly EndDate { get; private set; }
    public LeaseStatus Status { get; private set; }

    public decimal RentAmount { get; private set; }
    public decimal SecurityDeposit { get; private set; }
    public RentFrequency Frequency { get; private set; }
    public int DueDayOfMonth { get; private set; }
    public int GracePeriodDays { get; private set; }
    public decimal LateFeePercentage { get; private set; }

    public Guid? ParentLeaseId { get; private set; }
    public LeaseTerminationDetails? TerminationDetails { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public IReadOnlyCollection<LeaseResident> Residents => _residents.AsReadOnly();

    public LeaseDateRange DateRange => new(StartDate, EndDate);

    public LeaseFinancialTerms FinancialTerms => new(
        RentAmount,
        SecurityDeposit,
        Frequency,
        DueDayOfMonth,
        GracePeriodDays,
        LateFeePercentage);

    public static Lease CreateDraft(
        Guid organizationId,
        Guid unitId,
        DateOnly startDate,
        DateOnly endDate,
        LeaseFinancialTerms terms,
        DateTime utcNow,
        Guid? parentLeaseId = null)
    {
        if (organizationId == Guid.Empty) throw new ArgumentException("Organization ID cannot be empty.", nameof(organizationId));
        if (unitId == Guid.Empty) throw new ArgumentException("Unit ID cannot be empty.", nameof(unitId));
        ArgumentNullException.ThrowIfNull(terms, nameof(terms));

        var dateRange = new LeaseDateRange(startDate, endDate);

        return new Lease
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            UnitId = unitId,
            StartDate = dateRange.StartDate,
            EndDate = dateRange.EndDate,
            Status = LeaseStatus.Draft,
            RentAmount = terms.RentAmount,
            SecurityDeposit = terms.SecurityDeposit,
            Frequency = terms.Frequency,
            DueDayOfMonth = terms.DueDayOfMonth,
            GracePeriodDays = terms.GracePeriodDays,
            LateFeePercentage = terms.LateFeePercentage,
            ParentLeaseId = parentLeaseId == Guid.Empty ? null : parentLeaseId,
            CreatedAtUtc = utcNow
        };
    }

    public void UpdateDraftTerms(
        DateOnly startDate,
        DateOnly endDate,
        LeaseFinancialTerms terms,
        DateTime utcNow)
    {
        EnsureInState(LeaseStatus.Draft, "update terms of");
        ArgumentNullException.ThrowIfNull(terms, nameof(terms));

        var dateRange = new LeaseDateRange(startDate, endDate);

        StartDate = dateRange.StartDate;
        EndDate = dateRange.EndDate;
        RentAmount = terms.RentAmount;
        SecurityDeposit = terms.SecurityDeposit;
        Frequency = terms.Frequency;
        DueDayOfMonth = terms.DueDayOfMonth;
        GracePeriodDays = terms.GracePeriodDays;
        LateFeePercentage = terms.LateFeePercentage;
        UpdatedAtUtc = utcNow;
    }

    public LeaseResident AddResident(
        Guid residentId,
        LeaseResidentRole role,
        DateTime utcNow)
    {
        EnsureInState(LeaseStatus.Draft, "add resident to");
        if (residentId == Guid.Empty) throw new ArgumentException("Resident ID cannot be empty.", nameof(residentId));

        if (_residents.Any(r => r.ResidentId == residentId))
        {
            throw new InvalidOperationException($"Resident '{residentId}' is already associated with this lease.");
        }

        if (role == LeaseResidentRole.PrimaryTenant && _residents.Any(r => r.Role == LeaseResidentRole.PrimaryTenant))
        {
            throw new InvalidOperationException("A lease can have only one Primary Tenant. Change existing Primary Tenant first or assign CoTenant/Occupant.");
        }

        var leaseResident = LeaseResident.Create(OrganizationId, Id, residentId, role, utcNow);
        _residents.Add(leaseResident);
        UpdatedAtUtc = utcNow;
        return leaseResident;
    }

    public void RemoveResident(Guid residentId, DateTime utcNow)
    {
        EnsureInState(LeaseStatus.Draft, "remove resident from");

        var resident = _residents.FirstOrDefault(r => r.ResidentId == residentId)
            ?? throw new InvalidOperationException($"Resident '{residentId}' was not found on this lease.");

        _residents.Remove(resident);
        UpdatedAtUtc = utcNow;
    }

    public void SubmitForApproval(DateTime utcNow)
    {
        EnsureInState(LeaseStatus.Draft, "submit");

        if (!_residents.Any(r => r.Role == LeaseResidentRole.PrimaryTenant))
        {
            throw new InvalidOperationException("Cannot submit a lease for approval without at least one Primary Tenant.");
        }

        Status = LeaseStatus.PendingApproval;
        UpdatedAtUtc = utcNow;
    }

    public void Approve(DateTime utcNow)
    {
        EnsureInState(LeaseStatus.PendingApproval, "approve");

        // After approval, lease is ready for execution or instant activation if startDate <= today
        Status = LeaseStatus.Active;
        UpdatedAtUtc = utcNow;
    }

    public void Terminate(
        DateOnly terminationDate,
        string reason,
        Guid terminatedByUserId,
        DateTime utcNow,
        string? notes = null)
    {
        EnsureInState(LeaseStatus.Active, "terminate");

        if (terminationDate < StartDate)
        {
            throw new InvalidOperationException($"Termination date ({terminationDate}) cannot be earlier than lease StartDate ({StartDate}).");
        }

        if (terminationDate > EndDate)
        {
            throw new InvalidOperationException($"Termination date ({terminationDate}) cannot be later than lease EndDate ({EndDate}).");
        }

        TerminationDetails = new LeaseTerminationDetails(
            terminationDate: terminationDate,
            reason: reason,
            terminatedByUserId: terminatedByUserId,
            terminatedAtUtc: utcNow,
            notes: notes);

        Status = LeaseStatus.Terminated;
        UpdatedAtUtc = utcNow;
    }

    public void Cancel(DateTime utcNow)
    {
        if (Status != LeaseStatus.Draft && Status != LeaseStatus.PendingApproval)
        {
            throw new InvalidOperationException($"Cannot cancel lease in '{Status}' state. Only Draft and PendingApproval leases can be cancelled.");
        }

        Status = LeaseStatus.Cancelled;
        UpdatedAtUtc = utcNow;
    }

    public void Expire(DateTime utcNow)
    {
        EnsureInState(LeaseStatus.Active, "expire");

        Status = LeaseStatus.Expired;
        UpdatedAtUtc = utcNow;
    }

    public Lease CreateRenewal(
        DateOnly newEndDate,
        LeaseFinancialTerms? newTerms,
        DateTime utcNow)
    {
        if (Status != LeaseStatus.Active && Status != LeaseStatus.Expired)
        {
            throw new InvalidOperationException($"Cannot renew a lease in '{Status}' state. Lease must be Active or Expired to renew.");
        }

        var newStartDate = EndDate.AddDays(1);
        if (newEndDate <= newStartDate)
        {
            throw new InvalidOperationException($"Renewal EndDate ({newEndDate}) must be later than the new StartDate ({newStartDate}).");
        }

        var renewalTerms = newTerms ?? FinancialTerms;
        var newLease = CreateDraft(
            organizationId: OrganizationId,
            unitId: UnitId,
            startDate: newStartDate,
            endDate: newEndDate,
            terms: renewalTerms,
            utcNow: utcNow,
            parentLeaseId: Id);

        // Copy residents to the renewed lease
        foreach (var resident in _residents)
        {
            newLease.AddResident(resident.ResidentId, resident.Role, utcNow);
        }

        return newLease;
    }

    /// <summary>
    /// Checks if this lease can conflict/overlap with another lease for the same unit.
    /// Cancelled and Expired leases before start do not conflict.
    /// Terminated leases only occupy up to their TerminationDate.
    /// </summary>
    public bool OverlapsWith(DateOnly candidateStart, DateOnly candidateEnd)
    {
        if (Status == LeaseStatus.Cancelled)
        {
            return false;
        }

        var effectiveEnd = (Status == LeaseStatus.Terminated && TerminationDetails != null)
            ? TerminationDetails.TerminationDate
            : EndDate;

        var maxStart = StartDate > candidateStart ? StartDate : candidateStart;
        var minEnd = effectiveEnd < candidateEnd ? effectiveEnd : candidateEnd;

        return maxStart <= minEnd;
    }

    private void EnsureInState(LeaseStatus expectedStatus, string operation)
    {
        if (Status != expectedStatus)
        {
            throw new InvalidOperationException($"Cannot {operation} lease in '{Status}' state. Expected state: '{expectedStatus}'.");
        }
    }
}
