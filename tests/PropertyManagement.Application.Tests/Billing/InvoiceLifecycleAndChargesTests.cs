using System.Data;
using FluentValidation.TestHelper;
using NSubstitute;
using PropertyManagement.Application.Abstractions;
using PropertyManagement.Application.Abstractions.Audit;
using PropertyManagement.Application.Abstractions.Billing;
using PropertyManagement.Application.Abstractions.Ledger;
using PropertyManagement.Application.Abstractions.Repositories;
using PropertyManagement.Application.Common.Enums;
using PropertyManagement.Application.Features.Billing.Charges;
using PropertyManagement.Application.Features.Billing.Generate;
using PropertyManagement.Application.Features.Billing.Invoices;
using PropertyManagement.Application.Features.Billing.Preview;
using PropertyManagement.Domain.Entities.Billing;
using PropertyManagement.Domain.Entities.Leases;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Application.Tests.Billing;

public sealed class InvoiceLifecycleAndChargesTests
{
    private static readonly DateTime FixedNow = new(2026, 3, 1, 10, 0, 0, DateTimeKind.Utc);

    private static Lease CreateActiveLease(Guid orgId, string currency = "USD")
    {
        var lease = Lease.CreateDraft(
            orgId, Guid.NewGuid(), Guid.NewGuid(),
            new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31),
            new LeaseFinancialTerms(10000m, 20000m, currency: currency),
            FixedNow);
        lease.AddResident(Guid.NewGuid(), LeaseResidentRole.PrimaryTenant, FixedNow);
        lease.SubmitForApproval(FixedNow);
        lease.Approve(FixedNow);
        return lease;
    }

    private static Invoice CreateSampleDraftInvoice(Guid orgId, Guid leaseId, Guid residentId, decimal amount = 10000m)
    {
        var inv = Invoice.CreateDraft(
            orgId, leaseId, Guid.NewGuid(), Guid.NewGuid(), residentId,
            BillingPeriod.ForMonth(2026, 3),
            new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 5),
            "USD", FixedNow);
        inv.AddLine(ChargeType.Rent, "March Rent", 1m, amount, FixedNow);
        return inv;
    }

    [Fact]
    public async Task CreateAndVoidCharge_ShouldRespectPendingLifecycle()
    {
        var orgId = Guid.NewGuid();
        var lease = CreateActiveLease(orgId, currency: "EGP");

        var leaseRepo = Substitute.For<ILeaseRepository>();
        leaseRepo.GetByIdAsync(lease.Id, orgId, Arg.Any<CancellationToken>()).Returns(lease);
        var chargeRepo = Substitute.For<IChargeRepository>();
        var uow = Substitute.For<IUnitOfWork>();
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(FixedNow);

        var createHandler = new CreateChargeCommandHandler(leaseRepo, chargeRepo, uow, clock);
        var createRes = await createHandler.Handle(
            new CreateChargeCommand(orgId, lease.Id, ChargeType.Utility, "Electricity", 2m, 150m),
            CancellationToken.None);

        Assert.True(createRes.IsSuccess);
        Assert.Equal("EGP", createRes.Value.Currency);
        Assert.Equal(300m, createRes.Value.Amount);
        Assert.Equal(ChargeStatus.Pending, createRes.Value.Status);

        var charge = Charge.Create(orgId, lease.Id, lease.PropertyId, lease.UnitId, lease.Residents.First().ResidentId,
            ChargeType.Utility, "Electricity", 2m, 150m, "EGP", FixedNow);
        chargeRepo.GetByIdAsync(charge.Id, orgId, Arg.Any<CancellationToken>()).Returns(charge);

        var voidTx = Substitute.For<PropertyManagement.Application.Abstractions.IDbTransaction>();
        uow.BeginTransactionAsync(IsolationLevel.Serializable, Arg.Any<CancellationToken>()).Returns(voidTx);
        var voidHandler = new VoidChargeCommandHandler(chargeRepo, leaseRepo, uow, clock);
        var voidRes = await voidHandler.Handle(new VoidChargeCommand(orgId, charge.Id, "Entered by mistake"), CancellationToken.None);

        Assert.True(voidRes.IsSuccess);
        Assert.Equal(ChargeStatus.Voided, voidRes.Value.Status);
        Assert.Equal("Entered by mistake", voidRes.Value.VoidReason);

        // Voiding an already-voided (non-pending) charge returns Conflict
        var secondVoidRes = await voidHandler.Handle(new VoidChargeCommand(orgId, charge.Id, "Retry"), CancellationToken.None);
        Assert.True(secondVoidRes.IsFailure);
        Assert.Equal(ErrorType.Conflict, secondVoidRes.Errors.First().Type);
    }

    [Fact]
    public async Task CreateManualInvoice_ShouldCreateWithLinesAndAttachedCharge()
    {
        var orgId = Guid.NewGuid();
        var lease = CreateActiveLease(orgId, currency: "USD");
        var residentId = lease.Residents.First().ResidentId;
        var pendingCharge = Charge.Create(orgId, lease.Id, lease.PropertyId, lease.UnitId, residentId, ChargeType.Parking, "Spot B1", 1m, 200m, "USD", FixedNow);

        var leaseRepo = Substitute.For<ILeaseRepository>();
        leaseRepo.GetByIdAsync(lease.Id, orgId, Arg.Any<CancellationToken>()).Returns(lease);
        var chargeRepo = Substitute.For<IChargeRepository>();
        chargeRepo.GetByIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), orgId, Arg.Any<CancellationToken>())
            .Returns([pendingCharge]);
        var invoiceRepo = Substitute.For<IInvoiceRepository>();
        var numberGenerator = Substitute.For<IInvoiceNumberGenerator>();
        numberGenerator.GenerateNextAsync(orgId, 2026, Arg.Any<CancellationToken>()).Returns("INV-2026-000100");
        var uow = Substitute.For<IUnitOfWork>();
        var tx = Substitute.For<PropertyManagement.Application.Abstractions.IDbTransaction>();
        uow.BeginTransactionAsync(IsolationLevel.Serializable, Arg.Any<CancellationToken>()).Returns(tx);
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(FixedNow);

        var ledgerRepo = Substitute.For<ILedgerRepository>();
        var jeNumberGen = Substitute.For<IJournalEntryNumberGenerator>();
        jeNumberGen.GenerateNextAsync(orgId, Arg.Any<DateTime>(), Arg.Any<CancellationToken>()).Returns("JE-2026-000001");
        var auditRepo = Substitute.For<IFinancialAuditLogRepository>();
        var handler = new CreateManualInvoiceCommandHandler(leaseRepo, chargeRepo, invoiceRepo, numberGenerator, ledgerRepo, jeNumberGen, auditRepo, uow, clock);
        var result = await handler.Handle(
            new CreateManualInvoiceCommand(
                orgId,
                lease.Id,
                new DateOnly(2026, 3, 1),
                new DateOnly(2026, 3, 31),
                new DateOnly(2026, 3, 10),
                Lines: [new ManualInvoiceLineInput(ChargeType.OtherFee, "AC filter replacement", 1m, 150m)],
                ChargeIds: [pendingCharge.Id],
                AutoIssue: true),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(InvoiceStatus.Issued, result.Value.Status);
        Assert.Equal("INV-2026-000100", result.Value.InvoiceNumber);
        Assert.Equal(350m, result.Value.Total);
        Assert.Equal(ChargeStatus.Billed, pendingCharge.Status);
        await ledgerRepo.Received(1).AddAsync(
            Arg.Is<PropertyManagement.Domain.Entities.Ledger.JournalEntry>(j =>
                j.SourceType == "InvoiceIssued" && j.TotalDebits == 350m && j.TotalCredits == 350m),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task IssueAndVoidInvoice_ShouldAssignNumber_AndVoidShouldReleaseBilledCharges()
    {
        var orgId = Guid.NewGuid();
        var leaseId = Guid.NewGuid();
        var residentId = Guid.NewGuid();
        var invoice = CreateSampleDraftInvoice(orgId, leaseId, residentId);
        var billedCharge = Charge.Create(orgId, leaseId, invoice.PropertyId, invoice.UnitId, residentId, ChargeType.Utility, "Water", 1m, 120m, "USD", FixedNow);
        billedCharge.MarkBilled(invoice.Id, FixedNow);

        var invoiceRepo = Substitute.For<IInvoiceRepository>();
        invoiceRepo.GetByIdAsync(invoice.Id, orgId, Arg.Any<CancellationToken>()).Returns(invoice);
        var chargeRepo = Substitute.For<IChargeRepository>();
        chargeRepo.GetByInvoiceIdAsync(invoice.Id, orgId, Arg.Any<CancellationToken>()).Returns([billedCharge]);
        var numberGenerator = Substitute.For<IInvoiceNumberGenerator>();
        numberGenerator.GenerateNextAsync(orgId, 2026, Arg.Any<CancellationToken>()).Returns("INV-2026-000001");
        var uow = Substitute.For<IUnitOfWork>();
        var tx = Substitute.For<PropertyManagement.Application.Abstractions.IDbTransaction>();
        uow.BeginTransactionAsync(IsolationLevel.Serializable, Arg.Any<CancellationToken>()).Returns(tx);
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(FixedNow);

        var ledgerRepo = Substitute.For<ILedgerRepository>();
        var jeNumberGen = Substitute.For<IJournalEntryNumberGenerator>();
        jeNumberGen.GenerateNextAsync(orgId, Arg.Any<DateTime>(), Arg.Any<CancellationToken>()).Returns("JE-2026-000001", "JE-2026-000002");
        var auditRepo = Substitute.For<IFinancialAuditLogRepository>();
        var issueHandler = new IssueInvoiceCommandHandler(invoiceRepo, numberGenerator, ledgerRepo, jeNumberGen, auditRepo, uow, clock);
        var issueRes = await issueHandler.Handle(new IssueInvoiceCommand(orgId, invoice.Id), CancellationToken.None);
        Assert.True(issueRes.IsSuccess);
        Assert.Equal(InvoiceStatus.Issued, issueRes.Value.Status);
        Assert.Equal("INV-2026-000001", issueRes.Value.InvoiceNumber);

        var voidHandler = new VoidInvoiceCommandHandler(invoiceRepo, chargeRepo, ledgerRepo, jeNumberGen, auditRepo, uow, clock);
        var voidRes = await voidHandler.Handle(new VoidInvoiceCommand(orgId, invoice.Id, "Issued in error"), CancellationToken.None);
        Assert.True(voidRes.IsSuccess);
        await ledgerRepo.Received(1).AddAsync(
            Arg.Is<PropertyManagement.Domain.Entities.Ledger.JournalEntry>(j => j.SourceType == "InvoiceIssued" && j.TotalDebits == j.TotalCredits),
            Arg.Any<CancellationToken>());
        await ledgerRepo.Received(1).AddAsync(
            Arg.Is<PropertyManagement.Domain.Entities.Ledger.JournalEntry>(j => j.SourceType == "InvoiceVoided" && j.TotalDebits == j.TotalCredits),
            Arg.Any<CancellationToken>());
        Assert.Equal(InvoiceStatus.Void, voidRes.Value.Status);
        Assert.Equal(0m, voidRes.Value.BalanceDue);
        Assert.Equal(ChargeStatus.Pending, billedCharge.Status);
        Assert.Null(billedCharge.InvoiceId);
    }

    [Fact]
    public async Task ApplyAdjustment_ShouldAppendLateFeeAndCredit_AndRejectOverCredit()
    {
        var orgId = Guid.NewGuid();
        var invoice = CreateSampleDraftInvoice(orgId, Guid.NewGuid(), Guid.NewGuid(), amount: 10000m);
        invoice.Issue("INV-2026-000009", new DateOnly(2026, 3, 1), FixedNow);

        var invoiceRepo = Substitute.For<IInvoiceRepository>();
        invoiceRepo.GetByIdAsync(invoice.Id, orgId, Arg.Any<CancellationToken>()).Returns(invoice);
        var uow = Substitute.For<IUnitOfWork>();
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(FixedNow.AddDays(10));

        var ledgerRepo = Substitute.For<ILedgerRepository>();
        var jeNumberGen = Substitute.For<IJournalEntryNumberGenerator>();
        jeNumberGen.GenerateNextAsync(orgId, Arg.Any<DateTime>(), Arg.Any<CancellationToken>()).Returns("JE-2026-000010", "JE-2026-000011");
        var auditRepo = Substitute.For<IFinancialAuditLogRepository>();
        var handler = new ApplyInvoiceAdjustmentCommandHandler(invoiceRepo, ledgerRepo, jeNumberGen, auditRepo, uow, clock);
        var actorId = Guid.NewGuid();

        var lateFeeRes = await handler.Handle(
            new ApplyInvoiceAdjustmentCommand(
                orgId, invoice.Id, InvoiceAdjustmentType.Debit, ChargeType.LateFee,
                500m, "March Late Fee", "5 days past due", actorId),
            CancellationToken.None);

        Assert.True(lateFeeRes.IsSuccess);
        Assert.Equal(10500m, lateFeeRes.Value.Total);
        Assert.Equal(500m, lateFeeRes.Value.TotalAdjustments);
        Assert.Single(lateFeeRes.Value.Adjustments);

        var overCreditRes = await handler.Handle(
            new ApplyInvoiceAdjustmentCommand(
                orgId, invoice.Id, InvoiceAdjustmentType.Credit, ChargeType.CreditAdjustment,
                15000m, "Excess Credit", "Invalid", actorId),
            CancellationToken.None);

        Assert.True(overCreditRes.IsFailure);
        Assert.Equal(ErrorType.Validation, overCreditRes.Errors.First().Type);
    }

    [Fact]
    public async Task MarkOverdueInvoices_ShouldMarkOnlyPastDueIssuedOrPartiallyPaidInvoices()
    {
        var orgId = Guid.NewGuid();
        var dueInvoice = CreateSampleDraftInvoice(orgId, Guid.NewGuid(), Guid.NewGuid());
        dueInvoice.Issue("INV-2026-000001", new DateOnly(2026, 3, 1), FixedNow);

        var invoiceRepo = Substitute.For<IInvoiceRepository>();
        invoiceRepo.GetPagedAsync(Arg.Any<InvoiceFilter>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                var filter = ci.Arg<InvoiceFilter>();
                if (filter.Status == InvoiceStatus.Issued && filter.DueBefore == new DateOnly(2026, 3, 5))
                    return ([dueInvoice], 1);
                return ((IReadOnlyList<Invoice>)Array.Empty<Invoice>(), 0);
            });

        var uow = Substitute.For<IUnitOfWork>();
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(new DateTime(2026, 3, 6, 8, 0, 0, DateTimeKind.Utc));

        var handler = new MarkOverdueInvoicesCommandHandler(invoiceRepo, uow, clock);
        var result = await handler.Handle(new MarkOverdueInvoicesCommand(orgId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.MarkedCount);
        Assert.Contains(dueInvoice.Id, result.Value.MarkedInvoiceIds);
        Assert.Equal(InvoiceStatus.Overdue, dueInvoice.Status);
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetInvoicesAndGetById_ShouldReturnMappedDtos()
    {
        var orgId = Guid.NewGuid();
        var invoice = CreateSampleDraftInvoice(orgId, Guid.NewGuid(), Guid.NewGuid());

        var invoiceRepo = Substitute.For<IInvoiceRepository>();
        invoiceRepo.GetByIdAsync(invoice.Id, orgId, Arg.Any<CancellationToken>()).Returns(invoice);
        invoiceRepo.GetPagedAsync(Arg.Any<InvoiceFilter>(), Arg.Any<CancellationToken>())
            .Returns(([invoice], 1));

        var getByIdHandler = new GetInvoiceByIdQueryHandler(invoiceRepo);
        var byIdResult = await getByIdHandler.Handle(new GetInvoiceByIdQuery(orgId, invoice.Id), CancellationToken.None);
        Assert.True(byIdResult.IsSuccess);
        Assert.Equal(invoice.Id, byIdResult.Value.Id);

        var getPagedHandler = new GetInvoicesQueryHandler(invoiceRepo);
        var pagedResult = await getPagedHandler.Handle(new GetInvoicesQuery(orgId, PageNumber: 1, PageSize: 20), CancellationToken.None);
        Assert.True(pagedResult.IsSuccess);
        Assert.Single(pagedResult.Value.Items);
        Assert.Equal(1, pagedResult.Value.TotalCount);
    }

    [Fact]
    public void BillingValidators_ShouldRejectInvalidInputs()
    {
        var previewValidator = new PreviewBillingQueryValidator();
        var badPreview = previewValidator.TestValidate(new PreviewBillingQuery(
            Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 4, 1), new DateOnly(2026, 3, 1)));
        badPreview.ShouldHaveValidationErrorFor(x => x.PeriodEnd);

        var genValidator = new GenerateLeaseInvoiceCommandValidator();
        var emptyGen = genValidator.TestValidate(new GenerateLeaseInvoiceCommand(
            Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31),
            IncludeRent: false, IncludePendingCharges: false));
        Assert.False(emptyGen.IsValid);

        var adjValidator = new ApplyInvoiceAdjustmentCommandValidator();
        var badCredit = adjValidator.TestValidate(new ApplyInvoiceAdjustmentCommand(
            Guid.NewGuid(), Guid.NewGuid(), InvoiceAdjustmentType.Credit, ChargeType.LateFee,
            100m, "Desc", "Reason", Guid.NewGuid()));
        badCredit.ShouldHaveValidationErrorFor(x => x.ChargeType);

        var chargeValidator = new CreateChargeCommandValidator();
        var badCharge = chargeValidator.TestValidate(new CreateChargeCommand(
            Guid.NewGuid(), Guid.NewGuid(), ChargeType.Utility, "Water", 0m, 100m));
        badCharge.ShouldHaveValidationErrorFor(x => x.Quantity);

        var manualValidator = new CreateManualInvoiceCommandValidator();
        var emptyManual = manualValidator.TestValidate(new CreateManualInvoiceCommand(
            Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31),
            new DateOnly(2026, 3, 10)));
        Assert.False(emptyManual.IsValid);
    }

    [Fact]
    public async Task VoidCharge_ShouldAcquireUnitLock_AndRejectInvalidRowVersion()
    {
        var orgId = Guid.NewGuid();
        var lease = CreateActiveLease(orgId);
        var charge = Charge.Create(orgId, lease.Id, lease.PropertyId, lease.UnitId, lease.Residents.First().ResidentId,
            ChargeType.Utility, "Water", 1m, 200m, "USD", FixedNow);

        var chargeRepo = Substitute.For<IChargeRepository>();
        chargeRepo.GetByIdAsync(charge.Id, orgId, Arg.Any<CancellationToken>()).Returns(charge);
        var leaseRepo = Substitute.For<ILeaseRepository>();
        var uow = Substitute.For<IUnitOfWork>();
        var tx = Substitute.For<PropertyManagement.Application.Abstractions.IDbTransaction>();
        uow.BeginTransactionAsync(IsolationLevel.Serializable, Arg.Any<CancellationToken>()).Returns(tx);
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(FixedNow);

        var handler = new VoidChargeCommandHandler(chargeRepo, leaseRepo, uow, clock);

        var badRowVersionRes = await handler.Handle(
            new VoidChargeCommand(orgId, charge.Id, "Bad token", RowVersion: "not-base64!"),
            CancellationToken.None);

        Assert.True(badRowVersionRes.IsFailure);
        Assert.Equal("Charge.InvalidRowVersion", badRowVersionRes.Errors.First().Code);
        await leaseRepo.Received(1).AcquireUnitLockAsync(charge.UnitId, orgId, Arg.Any<CancellationToken>());
        await tx.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ApplyAdjustment_WhenFullCreditZeroesBalanceOnIssuedOrOverdueInvoice_ShouldTransitionToPaid()
    {
        var orgId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var issuedInvoice = CreateSampleDraftInvoice(orgId, Guid.NewGuid(), Guid.NewGuid(), amount: 5000m);
        issuedInvoice.Issue("INV-2026-000010", new DateOnly(2026, 3, 1), FixedNow);

        var overdueInvoice = CreateSampleDraftInvoice(orgId, Guid.NewGuid(), Guid.NewGuid(), amount: 4000m);
        overdueInvoice.Issue("INV-2026-000011", new DateOnly(2026, 3, 1), FixedNow);
        overdueInvoice.MarkOverdue(new DateOnly(2026, 3, 10), FixedNow.AddDays(9));

        var invoiceRepo = Substitute.For<IInvoiceRepository>();
        invoiceRepo.GetByIdAsync(issuedInvoice.Id, orgId, Arg.Any<CancellationToken>()).Returns(issuedInvoice);
        invoiceRepo.GetByIdAsync(overdueInvoice.Id, orgId, Arg.Any<CancellationToken>()).Returns(overdueInvoice);
        var uow = Substitute.For<IUnitOfWork>();
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(FixedNow.AddDays(10));

        var ledgerRepo = Substitute.For<ILedgerRepository>();
        var jeNumberGen = Substitute.For<IJournalEntryNumberGenerator>();
        jeNumberGen.GenerateNextAsync(orgId, Arg.Any<DateTime>(), Arg.Any<CancellationToken>()).Returns("JE-2026-000010", "JE-2026-000011");
        var auditRepo = Substitute.For<IFinancialAuditLogRepository>();
        var handler = new ApplyInvoiceAdjustmentCommandHandler(invoiceRepo, ledgerRepo, jeNumberGen, auditRepo, uow, clock);

        var issuedRes = await handler.Handle(
            new ApplyInvoiceAdjustmentCommand(
                orgId, issuedInvoice.Id, InvoiceAdjustmentType.Credit, ChargeType.CreditAdjustment,
                5000m, "Full waiver", "Move-in credit", actorId),
            CancellationToken.None);

        Assert.True(issuedRes.IsSuccess);
        Assert.Equal(0m, issuedRes.Value.BalanceDue);
        Assert.Equal(InvoiceStatus.Paid, issuedRes.Value.Status);

        var overdueRes = await handler.Handle(
            new ApplyInvoiceAdjustmentCommand(
                orgId, overdueInvoice.Id, InvoiceAdjustmentType.Credit, ChargeType.Discount,
                4000m, "Full overdue waiver", "Settlement agreement", actorId),
            CancellationToken.None);

        Assert.True(overdueRes.IsSuccess);
        Assert.Equal(0m, overdueRes.Value.BalanceDue);
        Assert.Equal(InvoiceStatus.Paid, overdueRes.Value.Status);
    }

    [Fact]
    public async Task GetInvoicesQuery_ShouldUseSummaryProjectionFlags_AndIssueDateYearValidation()
    {
        var orgId = Guid.NewGuid();
        var invoice = CreateSampleDraftInvoice(orgId, Guid.NewGuid(), Guid.NewGuid());
        var invoiceRepo = Substitute.For<IInvoiceRepository>();
        invoiceRepo.GetPagedAsync(Arg.Any<InvoiceFilter>(), Arg.Any<CancellationToken>())
            .Returns(([invoice], 1));

        var handler = new GetInvoicesQueryHandler(invoiceRepo);
        var res = await handler.Handle(new GetInvoicesQuery(orgId), CancellationToken.None);

        Assert.True(res.IsSuccess);
        await invoiceRepo.Received(1).GetPagedAsync(
            Arg.Is<InvoiceFilter>(f => !f.IncludeDetails && f.AsNoTracking),
            Arg.Any<CancellationToken>());

        var genVal = new GenerateLeaseInvoiceCommandValidator();
        var badGenYear = genVal.TestValidate(new GenerateLeaseInvoiceCommand(
            orgId, Guid.NewGuid(), new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31),
            IssueDate: new DateOnly(1999, 12, 31)));
        badGenYear.ShouldHaveValidationErrorFor(x => x.IssueDate);

        var issueVal = new IssueInvoiceCommandValidator();
        var badIssueYear = issueVal.TestValidate(new IssueInvoiceCommand(
            orgId, Guid.NewGuid(), IssueDate: new DateOnly(1999, 12, 31)));
        badIssueYear.ShouldHaveValidationErrorFor(x => x.IssueDate);
    }
}
