using System.Data;
using NSubstitute;
using PropertyManagement.Application.Abstractions;
using PropertyManagement.Application.Abstractions.Audit;
using PropertyManagement.Application.Abstractions.Billing;
using PropertyManagement.Application.Abstractions.Ledger;
using PropertyManagement.Application.Abstractions.Repositories;
using PropertyManagement.Application.Common.Enums;
using PropertyManagement.Application.Features.Billing.Common;
using PropertyManagement.Application.Features.Billing.Generate;
using PropertyManagement.Application.Features.Billing.Preview;
using PropertyManagement.Domain.Entities.Billing;
using PropertyManagement.Domain.Entities.Billing.Services;
using PropertyManagement.Domain.Entities.Leases;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Application.Tests.Billing;

public sealed class BillingPreviewAndGenerateTests
{
    private static readonly DateTime FixedNow = new(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc);
    private readonly IBillingCalculationEngine _engine = new BillingCalculationEngine();

    private static Lease CreateActiveLease(
        Guid orgId,
        DateOnly? start = null,
        DateOnly? end = null,
        decimal rent = 10000m,
        int dueDay = 5,
        string currency = "EGP")
    {
        var lease = Lease.CreateDraft(
            orgId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            start ?? new DateOnly(2026, 3, 1),
            end ?? new DateOnly(2027, 2, 28),
            new LeaseFinancialTerms(rent, rent * 2, RentFrequency.Monthly, dueDay, 5, 5m, currency),
            FixedNow);
        lease.AddResident(Guid.NewGuid(), LeaseResidentRole.PrimaryTenant, FixedNow);
        lease.SubmitForApproval(FixedNow);
        lease.Approve(FixedNow);
        return lease;
    }

    [Fact]
    public async Task PreviewBilling_ShouldCalculateProrationPendingChargesAndDiscount_WithoutPersisting()
    {
        var orgId = Guid.NewGuid();
        var lease = CreateActiveLease(orgId, start: new DateOnly(2026, 3, 16), end: new DateOnly(2027, 3, 15), rent: 3100m, dueDay: 5);
        var residentId = lease.Residents.First().ResidentId;

        var parkingCharge = Charge.Create(
            orgId, lease.Id, lease.PropertyId, lease.UnitId, residentId,
            ChargeType.Parking, "Parking March", 1m, 300m, "EGP", FixedNow,
            new DateOnly(2026, 3, 16), new DateOnly(2026, 3, 31));

        var leaseRepo = Substitute.For<ILeaseRepository>();
        leaseRepo.GetByIdAsync(lease.Id, orgId, Arg.Any<CancellationToken>()).Returns(lease);

        var chargeRepo = Substitute.For<IChargeRepository>();
        chargeRepo.GetPendingByLeaseAsync(lease.Id, orgId, new DateOnly(2026, 3, 31), Arg.Any<CancellationToken>())
            .Returns([parkingCharge]);

        var invoiceRepo = Substitute.For<IInvoiceRepository>();
        invoiceRepo.ExistsActiveByIdempotencyKeyAsync(Arg.Any<string>(), orgId, Arg.Any<CancellationToken>())
            .Returns(false);

        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(FixedNow);

        var handler = new PreviewBillingQueryHandler(leaseRepo, chargeRepo, invoiceRepo, _engine, clock);

        var result = await handler.Handle(
            new PreviewBillingQuery(
                orgId,
                lease.Id,
                new DateOnly(2026, 3, 1),
                new DateOnly(2026, 3, 31),
                IssueDate: new DateOnly(2026, 3, 16),
                Discounts: [new BillingDiscountRequest("Move-in promo", 100m)]),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.IsProrated);
        Assert.Equal(new DateOnly(2026, 3, 16), result.Value.EffectiveStart);
        Assert.Equal(new DateOnly(2026, 3, 31), result.Value.EffectiveEnd);
        Assert.Equal("EGP", result.Value.Currency);
        // 16/31 * 3100 = 1600 + 300 parking - 100 discount = 1800
        Assert.Equal(1800m, result.Value.Total);
        Assert.Equal(3, result.Value.Lines.Count);
        Assert.False(result.Value.AlreadyBilledForPeriod);
        Assert.Equal(ChargeStatus.Pending, parkingCharge.Status);
    }

    [Fact]
    public async Task GenerateLeaseInvoice_ShouldDefaultToDraft_MarkPendingChargesBilled_AndUseUnitLock()
    {
        var orgId = Guid.NewGuid();
        var lease = CreateActiveLease(orgId, rent: 10000m, dueDay: 5, currency: "USD");
        var residentId = lease.Residents.First().ResidentId;

        var waterCharge = Charge.Create(
            orgId, lease.Id, lease.PropertyId, lease.UnitId, residentId,
            ChargeType.Utility, "Water Bill", 1m, 250m, "USD", FixedNow);

        var leaseRepo = Substitute.For<ILeaseRepository>();
        leaseRepo.GetByIdAsync(lease.Id, orgId, Arg.Any<CancellationToken>()).Returns(lease);

        var chargeRepo = Substitute.For<IChargeRepository>();
        chargeRepo.GetPendingByLeaseAsync(lease.Id, orgId, new DateOnly(2026, 3, 31), Arg.Any<CancellationToken>())
            .Returns([waterCharge]);

        var invoiceRepo = Substitute.For<IInvoiceRepository>();
        invoiceRepo.ExistsActiveByIdempotencyKeyAsync(Arg.Any<string>(), orgId, Arg.Any<CancellationToken>())
            .Returns(false);

        var numberGenerator = Substitute.For<IInvoiceNumberGenerator>();
        var uow = Substitute.For<IUnitOfWork>();
        var tx = Substitute.For<PropertyManagement.Application.Abstractions.IDbTransaction>();
        uow.BeginTransactionAsync(IsolationLevel.Serializable, Arg.Any<CancellationToken>()).Returns(tx);
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(FixedNow);

        var handler = new GenerateLeaseInvoiceCommandHandler(
            leaseRepo, chargeRepo, invoiceRepo, numberGenerator, _engine,
            Substitute.For<ILedgerRepository>(), Substitute.For<IJournalEntryNumberGenerator>(), Substitute.For<IFinancialAuditLogRepository>(),
            uow, clock);

        var result = await handler.Handle(
            new GenerateLeaseInvoiceCommand(orgId, lease.Id, new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31)),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(InvoiceStatus.Draft, result.Value.Status);
        Assert.Null(result.Value.InvoiceNumber);
        Assert.Equal(10250m, result.Value.Total);
        Assert.Equal(2, result.Value.Lines.Count);
        Assert.Equal(ChargeStatus.Billed, waterCharge.Status);
        Assert.Equal(result.Value.Id, waterCharge.InvoiceId);

        await numberGenerator.DidNotReceive().GenerateNextAsync(Arg.Any<Guid>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
        await leaseRepo.Received(1).AcquireUnitLockAsync(lease.UnitId, orgId, Arg.Any<CancellationToken>());
        await invoiceRepo.Received(1).AddAsync(Arg.Any<Invoice>(), Arg.Any<CancellationToken>());
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await tx.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GenerateLeaseInvoice_WhenAutoIssueTrue_ShouldAssignInvoiceNumberAndIssue()
    {
        var orgId = Guid.NewGuid();
        var lease = CreateActiveLease(orgId);

        var leaseRepo = Substitute.For<ILeaseRepository>();
        leaseRepo.GetByIdAsync(lease.Id, orgId, Arg.Any<CancellationToken>()).Returns(lease);
        var chargeRepo = Substitute.For<IChargeRepository>();
        chargeRepo.GetPendingByLeaseAsync(lease.Id, orgId, Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns([]);
        var invoiceRepo = Substitute.For<IInvoiceRepository>();
        var numberGenerator = Substitute.For<IInvoiceNumberGenerator>();
        numberGenerator.GenerateNextAsync(orgId, 2026, Arg.Any<CancellationToken>()).Returns("INV-2026-000042");
        var ledgerRepo = Substitute.For<ILedgerRepository>();
        var jeNumberGen = Substitute.For<IJournalEntryNumberGenerator>();
        jeNumberGen.GenerateNextAsync(orgId, Arg.Any<DateTime>(), Arg.Any<CancellationToken>()).Returns("JE-2026-000042");
        var auditRepo = Substitute.For<IFinancialAuditLogRepository>();

        var uow = Substitute.For<IUnitOfWork>();
        var tx = Substitute.For<PropertyManagement.Application.Abstractions.IDbTransaction>();
        uow.BeginTransactionAsync(IsolationLevel.Serializable, Arg.Any<CancellationToken>()).Returns(tx);
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(FixedNow);

        var handler = new GenerateLeaseInvoiceCommandHandler(
            leaseRepo, chargeRepo, invoiceRepo, numberGenerator, _engine,
            ledgerRepo, jeNumberGen, auditRepo,
            uow, clock);

        var result = await handler.Handle(
            new GenerateLeaseInvoiceCommand(orgId, lease.Id, new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31), AutoIssue: true),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(InvoiceStatus.Issued, result.Value.Status);
        Assert.Equal("INV-2026-000042", result.Value.InvoiceNumber);
        Assert.Equal(FixedNow, result.Value.IssuedAtUtc);
        await ledgerRepo.Received(1).AddAsync(
            Arg.Is<PropertyManagement.Domain.Entities.Ledger.JournalEntry>(j =>
                j.SourceType == "InvoiceIssued" && j.TotalDebits == j.TotalCredits),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GenerateLeaseInvoice_WhenAlreadyBilled_ShouldReturnConflictWithoutSaving()
    {
        var orgId = Guid.NewGuid();
        var lease = CreateActiveLease(orgId);

        var leaseRepo = Substitute.For<ILeaseRepository>();
        leaseRepo.GetByIdAsync(lease.Id, orgId, Arg.Any<CancellationToken>()).Returns(lease);
        var chargeRepo = Substitute.For<IChargeRepository>();
        var invoiceRepo = Substitute.For<IInvoiceRepository>();
        var expectedKey = $"RENT:{lease.Id:N}:20260301:20260331";
        invoiceRepo.ExistsActiveByIdempotencyKeyAsync(expectedKey, orgId, Arg.Any<CancellationToken>()).Returns(true);

        var numberGenerator = Substitute.For<IInvoiceNumberGenerator>();
        var uow = Substitute.For<IUnitOfWork>();
        var tx = Substitute.For<PropertyManagement.Application.Abstractions.IDbTransaction>();
        uow.BeginTransactionAsync(IsolationLevel.Serializable, Arg.Any<CancellationToken>()).Returns(tx);
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(FixedNow);

        var handler = new GenerateLeaseInvoiceCommandHandler(
            leaseRepo, chargeRepo, invoiceRepo, numberGenerator, _engine,
            Substitute.For<ILedgerRepository>(), Substitute.For<IJournalEntryNumberGenerator>(), Substitute.For<IFinancialAuditLogRepository>(),
            uow, clock);

        var result = await handler.Handle(
            new GenerateLeaseInvoiceCommand(orgId, lease.Id, new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31)),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Billing.DuplicateInvoice", result.Errors.First().Code);
        Assert.Equal(ErrorType.Conflict, result.Errors.First().Type);
        await uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        await tx.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PreviewBilling_WhenLeaseIsDraft_ShouldReturnValidationFailure()
    {
        var orgId = Guid.NewGuid();
        var lease = Lease.CreateDraft(
            orgId, Guid.NewGuid(), Guid.NewGuid(),
            new DateOnly(2026, 3, 1), new DateOnly(2027, 2, 28),
            new LeaseFinancialTerms(10000m, 20000m), FixedNow);
        lease.AddResident(Guid.NewGuid(), LeaseResidentRole.PrimaryTenant, FixedNow);

        var leaseRepo = Substitute.For<ILeaseRepository>();
        leaseRepo.GetByIdAsync(lease.Id, orgId, Arg.Any<CancellationToken>()).Returns(lease);
        var chargeRepo = Substitute.For<IChargeRepository>();
        var invoiceRepo = Substitute.For<IInvoiceRepository>();
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(FixedNow);

        var handler = new PreviewBillingQueryHandler(leaseRepo, chargeRepo, invoiceRepo, _engine, clock);
        var result = await handler.Handle(
            new PreviewBillingQuery(orgId, lease.Id, new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31)),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Lease.NotBillable", result.Errors.First().Code);
        Assert.Equal(ErrorType.Validation, result.Errors.First().Type);
    }

    [Fact]
    public async Task GenerateLeaseInvoice_WithCustomIdempotencyKey_WhenRentPeriodOverlaps_ShouldReturnRentAlreadyBilledConflict()
    {
        var orgId = Guid.NewGuid();
        var lease = CreateActiveLease(orgId);

        var leaseRepo = Substitute.For<ILeaseRepository>();
        leaseRepo.GetByIdAsync(lease.Id, orgId, Arg.Any<CancellationToken>()).Returns(lease);
        var chargeRepo = Substitute.For<IChargeRepository>();
        var invoiceRepo = Substitute.For<IInvoiceRepository>();
        invoiceRepo.ExistsActiveByIdempotencyKeyAsync("custom-key-2", orgId, Arg.Any<CancellationToken>()).Returns(false);
        invoiceRepo.ExistsActiveRentOverlapAsync(lease.Id, orgId, new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 30), Arg.Any<CancellationToken>())
            .Returns(true);

        var numberGenerator = Substitute.For<IInvoiceNumberGenerator>();
        var uow = Substitute.For<IUnitOfWork>();
        var tx = Substitute.For<PropertyManagement.Application.Abstractions.IDbTransaction>();
        uow.BeginTransactionAsync(IsolationLevel.Serializable, Arg.Any<CancellationToken>()).Returns(tx);
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(FixedNow);

        var handler = new GenerateLeaseInvoiceCommandHandler(
            leaseRepo, chargeRepo, invoiceRepo, numberGenerator, _engine,
            Substitute.For<ILedgerRepository>(), Substitute.For<IJournalEntryNumberGenerator>(), Substitute.For<IFinancialAuditLogRepository>(),
            uow, clock);

        var result = await handler.Handle(
            new GenerateLeaseInvoiceCommand(
                orgId,
                lease.Id,
                new DateOnly(2026, 3, 1),
                new DateOnly(2026, 3, 30),
                IdempotencyKey: "custom-key-2"),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Billing.RentAlreadyBilled", result.Errors.First().Code);
        Assert.Equal(ErrorType.Conflict, result.Errors.First().Type);
        await uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        await tx.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PreviewBilling_WhenRentPeriodOverlaps_ShouldReportAlreadyBilledForPeriod()
    {
        var orgId = Guid.NewGuid();
        var lease = CreateActiveLease(orgId);

        var leaseRepo = Substitute.For<ILeaseRepository>();
        leaseRepo.GetByIdAsync(lease.Id, orgId, Arg.Any<CancellationToken>()).Returns(lease);
        var chargeRepo = Substitute.For<IChargeRepository>();
        chargeRepo.GetPendingByLeaseAsync(lease.Id, orgId, Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns([]);
        var invoiceRepo = Substitute.For<IInvoiceRepository>();
        invoiceRepo.ExistsActiveByIdempotencyKeyAsync(Arg.Any<string>(), orgId, Arg.Any<CancellationToken>()).Returns(false);
        invoiceRepo.ExistsActiveRentOverlapAsync(lease.Id, orgId, new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 30), Arg.Any<CancellationToken>())
            .Returns(true);

        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(FixedNow);

        var handler = new PreviewBillingQueryHandler(leaseRepo, chargeRepo, invoiceRepo, _engine, clock);
        var result = await handler.Handle(
            new PreviewBillingQuery(orgId, lease.Id, new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 30)),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.AlreadyBilledForPeriod);
    }

    [Fact]
    public async Task GenerateLeaseInvoice_WhenAutoIssueThrowsDomainOrRangeError_ShouldReturnValidationFailure()
    {
        var orgId = Guid.NewGuid();
        var lease = CreateActiveLease(orgId);

        var leaseRepo = Substitute.For<ILeaseRepository>();
        leaseRepo.GetByIdAsync(lease.Id, orgId, Arg.Any<CancellationToken>()).Returns(lease);
        var chargeRepo = Substitute.For<IChargeRepository>();
        chargeRepo.GetPendingByLeaseAsync(lease.Id, orgId, Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns([]);
        var invoiceRepo = Substitute.For<IInvoiceRepository>();
        var numberGenerator = Substitute.For<IInvoiceNumberGenerator>();
        numberGenerator.GenerateNextAsync(orgId, 2026, Arg.Any<CancellationToken>())
            .Returns<Task<string>>(_ => throw new ArgumentOutOfRangeException("year"));

        var uow = Substitute.For<IUnitOfWork>();
        var tx = Substitute.For<PropertyManagement.Application.Abstractions.IDbTransaction>();
        uow.BeginTransactionAsync(IsolationLevel.Serializable, Arg.Any<CancellationToken>()).Returns(tx);
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(FixedNow);

        var handler = new GenerateLeaseInvoiceCommandHandler(
            leaseRepo, chargeRepo, invoiceRepo, numberGenerator, _engine,
            Substitute.For<ILedgerRepository>(), Substitute.For<IJournalEntryNumberGenerator>(), Substitute.For<IFinancialAuditLogRepository>(),
            uow, clock);

        var result = await handler.Handle(
            new GenerateLeaseInvoiceCommand(orgId, lease.Id, new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31), AutoIssue: true),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Invoice.IssueError", result.Errors.First().Code);
        Assert.Equal(ErrorType.Validation, result.Errors.First().Type);
        await uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        await tx.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }
}
