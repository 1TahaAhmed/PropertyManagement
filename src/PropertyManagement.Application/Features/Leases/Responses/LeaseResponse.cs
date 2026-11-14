using PropertyManagement.Domain.Entities.Leases;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Application.Features.Leases.Responses;

public sealed record LeaseResidentResponse(
    Guid Id,
    Guid ResidentId,
    LeaseResidentRole Role,
    DateTime AddedAtUtc,
    bool IsFinanciallyResponsible,
    bool CanManageLease,
    bool IsOccupant);

public sealed record LeaseResponse(
    Guid Id,
    Guid OrganizationId,
    Guid PropertyId,
    Guid UnitId,
    DateOnly StartDate,
    DateOnly EndDate,
    LeaseStatus Status,
    decimal RentAmount,
    decimal SecurityDeposit,
    RentFrequency Frequency,
    int DueDayOfMonth,
    int GracePeriodDays,
    decimal LateFeePercentage,
    Guid? ParentLeaseId,
    DateOnly? TerminationDate,
    string? TerminationReason,
    string? TerminationNotes,
    IReadOnlyList<LeaseResidentResponse> Residents,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    string RowVersion)
{
    public static LeaseResponse FromEntity(Lease lease)
    {
        return new LeaseResponse(
            lease.Id,
            lease.OrganizationId,
            lease.PropertyId,
            lease.UnitId,
            lease.StartDate,
            lease.EndDate,
            lease.Status,
            lease.RentAmount,
            lease.SecurityDeposit,
            lease.Frequency,
            lease.DueDayOfMonth,
            lease.GracePeriodDays,
            lease.LateFeePercentage,
            lease.ParentLeaseId,
            lease.TerminationDetails?.TerminationDate,
            lease.TerminationDetails?.Reason,
            lease.TerminationDetails?.Notes,
            lease.Residents.Select(r => new LeaseResidentResponse(
                r.Id,
                r.ResidentId,
                r.Role,
                r.AddedAtUtc,
                r.IsFinanciallyResponsible,
                r.CanManageLease,
                r.IsOccupant)).ToList(),
            lease.CreatedAtUtc,
            lease.UpdatedAtUtc,
            Convert.ToBase64String(lease.RowVersion));
    }
}
