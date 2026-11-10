using PropertyManagement.Domain.Entities.Leases;
using PropertyManagement.Domain.Enums;
using Xunit;

namespace PropertyManagement.Domain.Tests.Leases;

public class LeaseAggregateTests
{
    private readonly Guid _orgId = Guid.NewGuid();
    private readonly Guid _propertyId = Guid.NewGuid();
    private readonly Guid _unitId = Guid.NewGuid();
    private readonly DateOnly _startDate = new(2026, 1, 1);
    private readonly DateOnly _endDate = new(2026, 12, 31);
    private readonly DateTime _utcNow = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private LeaseFinancialTerms DefaultTerms => new(
        rentAmount: 10000m,
        securityDeposit: 15000m,
        frequency: RentFrequency.Monthly,
        dueDayOfMonth: 1,
        gracePeriodDays: 5,
        lateFeePercentage: 2.5m);

    [Fact]
    public void CreateDraft_WithValidData_InitializesInDraftState()
    {
        // Act
        var lease = Lease.CreateDraft(_orgId, _propertyId, _unitId, _startDate, _endDate, DefaultTerms, _utcNow);

        // Assert
        Assert.Equal(LeaseStatus.Draft, lease.Status);
        Assert.Equal(_orgId, lease.OrganizationId);
        Assert.Equal(_propertyId, lease.PropertyId);
        Assert.Equal(_unitId, lease.UnitId);
        Assert.Equal(_startDate, lease.StartDate);
        Assert.Equal(_endDate, lease.EndDate);
        Assert.Equal(10000m, lease.RentAmount);
        Assert.Empty(lease.Residents);
    }

    [Fact]
    public void CreateDraft_WithStartDateEqualOrAfterEndDate_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            Lease.CreateDraft(_orgId, _propertyId, _unitId, _endDate, _startDate, DefaultTerms, _utcNow));

        Assert.Throws<ArgumentException>(() =>
            Lease.CreateDraft(_orgId, _propertyId, _unitId, _startDate, _startDate, DefaultTerms, _utcNow));
    }

    [Fact]
    public void AddResident_InDraftState_AddsSuccessfully()
    {
        // Arrange
        var lease = Lease.CreateDraft(_orgId, _propertyId, _unitId, _startDate, _endDate, DefaultTerms, _utcNow);
        var residentId = Guid.NewGuid();

        // Act
        var lr = lease.AddResident(residentId, LeaseResidentRole.PrimaryTenant, _utcNow);

        // Assert
        Assert.Single(lease.Residents);
        Assert.Equal(residentId, lr.ResidentId);
        Assert.Equal(LeaseResidentRole.PrimaryTenant, lr.Role);
    }

    [Fact]
    public void AddResident_DuplicateResident_ThrowsInvalidOperationException()
    {
        // Arrange
        var lease = Lease.CreateDraft(_orgId, _propertyId, _unitId, _startDate, _endDate, DefaultTerms, _utcNow);
        var residentId = Guid.NewGuid();
        lease.AddResident(residentId, LeaseResidentRole.PrimaryTenant, _utcNow);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() =>
            lease.AddResident(residentId, LeaseResidentRole.CoTenant, _utcNow));
    }

    [Fact]
    public void AddResident_MultiplePrimaryTenants_ThrowsInvalidOperationException()
    {
        // Arrange
        var lease = Lease.CreateDraft(_orgId, _propertyId, _unitId, _startDate, _endDate, DefaultTerms, _utcNow);
        lease.AddResident(Guid.NewGuid(), LeaseResidentRole.PrimaryTenant, _utcNow);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() =>
            lease.AddResident(Guid.NewGuid(), LeaseResidentRole.PrimaryTenant, _utcNow));
    }

    [Fact]
    public void SubmitForApproval_WithoutPrimaryTenant_ThrowsInvalidOperationException()
    {
        // Arrange
        var lease = Lease.CreateDraft(_orgId, _propertyId, _unitId, _startDate, _endDate, DefaultTerms, _utcNow);
        lease.AddResident(Guid.NewGuid(), LeaseResidentRole.Occupant, _utcNow);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => lease.SubmitForApproval(_utcNow));
    }

    [Fact]
    public void FullLifecycle_Draft_PendingApproval_Active_Terminated()
    {
        // Arrange
        var lease = Lease.CreateDraft(_orgId, _propertyId, _unitId, _startDate, _endDate, DefaultTerms, _utcNow);
        lease.AddResident(Guid.NewGuid(), LeaseResidentRole.PrimaryTenant, _utcNow);

        // 1. Submit
        lease.SubmitForApproval(_utcNow);
        Assert.Equal(LeaseStatus.PendingApproval, lease.Status);

        // 2. Approve
        lease.Approve(_utcNow);
        Assert.Equal(LeaseStatus.Active, lease.Status);

        // 3. Terminate
        var terminationDate = new DateOnly(2026, 9, 15);
        lease.Terminate(terminationDate, "Tenant relocated", Guid.NewGuid(), _utcNow);

        Assert.Equal(LeaseStatus.Terminated, lease.Status);
        Assert.NotNull(lease.TerminationDetails);
        Assert.Equal(terminationDate, lease.TerminationDetails.TerminationDate);
        Assert.Equal("Tenant relocated", lease.TerminationDetails.Reason);
    }

    [Theory]
    [InlineData("2026-01-01", "2026-06-30", "2026-07-01", "2026-12-31", false)] // Adjacent, no overlap
    [InlineData("2026-01-01", "2026-07-01", "2026-07-01", "2026-12-31", true)]  // Exact boundary overlap on July 1
    [InlineData("2026-01-01", "2026-12-31", "2026-03-01", "2026-08-31", true)]  // Contained within
    [InlineData("2026-03-01", "2026-08-31", "2026-01-01", "2026-12-31", true)]  // Enclosing
    [InlineData("2026-05-01", "2026-12-31", "2026-01-01", "2026-06-01", true)]  // Overlap on start
    public void OverlapsWith_CorrectlyCalculatesTemporalOverlap(
        string startA, string endA, string startB, string endB, bool expectedOverlap)
    {
        // Arrange
        var rangeA = new LeaseDateRange(DateOnly.Parse(startA), DateOnly.Parse(endA));
        var rangeB = new LeaseDateRange(DateOnly.Parse(startB), DateOnly.Parse(endB));

        // Act & Assert
        Assert.Equal(expectedOverlap, rangeA.OverlapsWith(rangeB));
        Assert.Equal(expectedOverlap, rangeB.OverlapsWith(rangeA));
    }

    [Fact]
    public void OverlapsWith_WhenLeaseIsTerminated_UsesActualTerminationDate()
    {
        // Arrange: Lease originally scheduled Jan 1 -> Dec 31, but terminated on June 30
        var lease = Lease.CreateDraft(_orgId, _propertyId, _unitId, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), DefaultTerms, _utcNow);
        lease.AddResident(Guid.NewGuid(), LeaseResidentRole.PrimaryTenant, _utcNow);
        lease.SubmitForApproval(_utcNow);
        lease.Approve(_utcNow);
        lease.Terminate(new DateOnly(2026, 6, 30), "Early departure", Guid.NewGuid(), _utcNow);

        // Candidate lease starts July 1 -> Dec 31
        var candidateStart = new DateOnly(2026, 7, 1);
        var candidateEnd = new DateOnly(2026, 12, 31);

        // Act
        var overlaps = lease.OverlapsWith(candidateStart, candidateEnd);

        // Assert: Should NOT overlap because the previous lease was terminated on June 30
        Assert.False(overlaps);
    }

    [Fact]
    public void CreateRenewal_FromActiveLease_CopiesResidentsAndSetsConsecutiveDates()
    {
        // Arrange
        var lease = Lease.CreateDraft(_orgId, _propertyId, _unitId, _startDate, _endDate, DefaultTerms, _utcNow);
        var primaryId = Guid.NewGuid();
        lease.AddResident(primaryId, LeaseResidentRole.PrimaryTenant, _utcNow);
        lease.SubmitForApproval(_utcNow);
        lease.Approve(_utcNow);

        var nextYearEnd = new DateOnly(2027, 12, 31);

        // Act
        var renewal = lease.CreateRenewal(nextYearEnd, null, _utcNow);

        // Assert
        Assert.Equal(LeaseStatus.Draft, renewal.Status);
        Assert.Equal(new DateOnly(2027, 1, 1), renewal.StartDate); // Day right after 2026-12-31
        Assert.Equal(nextYearEnd, renewal.EndDate);
        Assert.Equal(lease.Id, renewal.ParentLeaseId);
        Assert.Single(renewal.Residents);
        Assert.Equal(primaryId, renewal.Residents.First().ResidentId);
    }
}
