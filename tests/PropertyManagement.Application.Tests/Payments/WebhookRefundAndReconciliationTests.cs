using System.Data;
using NSubstitute;
using PropertyManagement.Application.Abstractions;
using PropertyManagement.Application.Abstractions.Audit;
using PropertyManagement.Application.Abstractions.Billing;
using PropertyManagement.Application.Abstractions.Ledger;
using PropertyManagement.Application.Abstractions.Payments;
using PropertyManagement.Application.Abstractions.Security;
using PropertyManagement.Application.Common.Enums;
using PropertyManagement.Application.Features.Payments.Reconciliation;
using PropertyManagement.Application.Features.Payments.Refunds;
using PropertyManagement.Application.Features.Ledger.Queries;
using PropertyManagement.Application.Features.Payments.Queries;
using PropertyManagement.Application.Features.Payments.Webhooks;
using PropertyManagement.Domain.Entities.Billing;
using PropertyManagement.Domain.Entities.Ledger;
using PropertyManagement.Domain.Entities.Payments;
using PropertyManagement.Domain.Entities.Payments.Services;
using PropertyManagement.Domain.Enums;
using AppDbTransaction = PropertyManagement.Application.Abstractions.IDbTransaction;

namespace PropertyManagement.Application.Tests.Payments;

public sealed class WebhookRefundAndReconciliationTests
{
    private static readonly DateTime FixedNow = new(2026, 6, 15, 12, 0, 0, DateTimeKind.Utc);

    private static (
        IPaymentRepository PayRepo,
        IPaymentIntentRepository IntentRepo,
        IWebhookEventRepository WebhookRepo,
        IPaymentReconciliationRepository ReconRepo,
        IInvoiceRepository InvoiceRepo,
        ILedgerRepository LedgerRepo,
        IJournalEntryNumberGenerator JeNumGen,
        IFinancialAuditLogRepository AuditRepo,
        IPaymentProviderFactory ProviderFactory,
        IPaymentProvider ExternalProvider,
        ISystemTenantContext SystemTenant,
        IUnitOfWork Uow,
        IClock Clock,
        List<JournalEntry> CapturedJes,
        List<ProcessedWebhookEvent> CapturedWebhooks,
        List<PaymentReconciliationRecord> CapturedRecons) CreateMocks()
    {
        var payRepo = Substitute.For<IPaymentRepository>();
        var intentRepo = Substitute.For<IPaymentIntentRepository>();
        var webhookRepo = Substitute.For<IWebhookEventRepository>();
        var reconRepo = Substitute.For<IPaymentReconciliationRepository>();
        var invoiceRepo = Substitute.For<IInvoiceRepository>();
        var ledgerRepo = Substitute.For<ILedgerRepository>();
        var jeNumGen = Substitute.For<IJournalEntryNumberGenerator>();
        jeNumGen.GenerateNextAsync(Arg.Any<Guid>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns("JE-2026-000101", "JE-2026-000102", "JE-2026-000103");
        var auditRepo = Substitute.For<IFinancialAuditLogRepository>();
        var providerFactory = Substitute.For<IPaymentProviderFactory>();
        var externalProvider = Substitute.For<IPaymentProvider>();
        providerFactory.Resolve(Arg.Any<PaymentProviderType>()).Returns(externalProvider);

        var sysTenant = Substitute.For<ISystemTenantContext>();
        sysTenant.BeginScope(Arg.Any<Guid>()).Returns(Substitute.For<IDisposable>());

        var tx = Substitute.For<AppDbTransaction>();
        var uow = Substitute.For<IUnitOfWork>();
        uow.BeginTransactionAsync(IsolationLevel.Serializable, Arg.Any<CancellationToken>()).Returns(tx);

        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(FixedNow);

        var jes = new List<JournalEntry>();
        ledgerRepo.When(x => x.AddAsync(Arg.Any<JournalEntry>(), Arg.Any<CancellationToken>())).Do(c => jes.Add(c.Arg<JournalEntry>()));

        var hooks = new List<ProcessedWebhookEvent>();
        webhookRepo.When(x => x.AddAsync(Arg.Any<ProcessedWebhookEvent>(), Arg.Any<CancellationToken>())).Do(c => hooks.Add(c.Arg<ProcessedWebhookEvent>()));

        var recons = new List<PaymentReconciliationRecord>();
        reconRepo.When(x => x.AddRangeAsync(Arg.Any<IEnumerable<PaymentReconciliationRecord>>(), Arg.Any<CancellationToken>()))
            .Do(c => recons.AddRange(c.Arg<IEnumerable<PaymentReconciliationRecord>>()));

        return (payRepo, intentRepo, webhookRepo, reconRepo, invoiceRepo, ledgerRepo, jeNumGen, auditRepo, providerFactory, externalProvider, sysTenant, uow, clock, jes, hooks, recons);
    }

    [Fact]
    public async Task ProcessWebhook_InvalidSignature_ShouldReturnUnauthorizedWithoutSideEffects()
    {
        var m = CreateMocks();
        m.ExternalProvider.VerifyAndParseWebhook(Arg.Any<string>(), Arg.Any<IReadOnlyDictionary<string, string>>(), FixedNow)
            .Returns(new ProviderWebhookVerificationResult(false, false, "Bad signature", null, null, FixedNow, null, null, null, null, null, null, null, null, null));

        var handler = new ProcessPaymentWebhookCommandHandler(
            m.ProviderFactory, m.WebhookRepo, m.PayRepo, m.IntentRepo, m.InvoiceRepo,
            m.LedgerRepo, m.JeNumGen, m.AuditRepo, m.SystemTenant, m.Uow, m.Clock);

        var res = await handler.Handle(
            new ProcessPaymentWebhookCommand(PaymentProviderType.Stripe, "{}", new Dictionary<string, string>()),
            CancellationToken.None);

        Assert.False(res.IsSuccess);
        Assert.Equal(ErrorType.Unauthorized, res.Errors.First().Type);
        Assert.Empty(m.CapturedWebhooks);
    }

    [Fact]
    public async Task ProcessWebhook_ReplayOutsideToleranceWindow_ShouldReturnValidationReplayRejected()
    {
        var m = CreateMocks();
        m.ExternalProvider.VerifyAndParseWebhook(Arg.Any<string>(), Arg.Any<IReadOnlyDictionary<string, string>>(), FixedNow)
            .Returns(new ProviderWebhookVerificationResult(true, false, "Replay detected", "evt_old", "payment.succeeded", FixedNow.AddHours(-2), null, null, null, null, null, null, null, null, null));

        var handler = new ProcessPaymentWebhookCommandHandler(
            m.ProviderFactory, m.WebhookRepo, m.PayRepo, m.IntentRepo, m.InvoiceRepo,
            m.LedgerRepo, m.JeNumGen, m.AuditRepo, m.SystemTenant, m.Uow, m.Clock);

        var res = await handler.Handle(
            new ProcessPaymentWebhookCommand(PaymentProviderType.Stripe, "{}", new Dictionary<string, string>()),
            CancellationToken.None);

        Assert.False(res.IsSuccess);
        Assert.Equal("Webhook.ReplayRejected", res.Errors.First().Code);
        Assert.Equal(ErrorType.Validation, res.Errors.First().Type);
    }

    [Fact]
    public async Task ProcessWebhook_DuplicateEvent_ShouldReturnIgnoredDuplicateWithoutSideEffects()
    {
        var m = CreateMocks();
        m.ExternalProvider.VerifyAndParseWebhook(Arg.Any<string>(), Arg.Any<IReadOnlyDictionary<string, string>>(), FixedNow)
            .Returns(new ProviderWebhookVerificationResult(true, true, null, "evt_dup_1", "payment.succeeded", FixedNow, "pi_1", null, "ch_1", null, 5000m, 150m, "EGP", "succeeded", null));
        m.WebhookRepo.GetByProviderEventIdAsync(PaymentProviderType.Stripe, "evt_dup_1", Arg.Any<CancellationToken>())
            .Returns(ProcessedWebhookEvent.CreateReceived(PaymentProviderType.Stripe, "evt_dup_1", "payment.succeeded", "HASH", FixedNow, FixedNow));

        var handler = new ProcessPaymentWebhookCommandHandler(
            m.ProviderFactory, m.WebhookRepo, m.PayRepo, m.IntentRepo, m.InvoiceRepo,
            m.LedgerRepo, m.JeNumGen, m.AuditRepo, m.SystemTenant, m.Uow, m.Clock);

        var res = await handler.Handle(
            new ProcessPaymentWebhookCommand(PaymentProviderType.Stripe, "{\"id\":\"evt_dup_1\"}", new Dictionary<string, string>()),
            CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.True(res.Value.IsDuplicate);
        Assert.Equal(WebhookEventStatus.IgnoredDuplicate, res.Value.Status);
        Assert.Empty(m.CapturedJes);
        Assert.Empty(m.CapturedWebhooks);
    }

    [Fact]
    public async Task ProcessWebhook_OutOfOrderFailedAfterSucceeded_AndSuccessAutoAllocation_ShouldBeSafe()
    {
        var m = CreateMocks();
        var orgId = Guid.NewGuid();
        var residentId = Guid.NewGuid();
        var inv = Invoice.CreateDraft(orgId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), residentId,
            BillingPeriod.ForMonth(2026, 6), new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 5), "EGP", FixedNow);
        inv.AddLine(ChargeType.Rent, "Rent", 1m, 5000m, FixedNow);
        inv.Issue("INV-WH-1", new DateOnly(2026, 6, 1), FixedNow);

        var payment = Payment.Create(orgId, residentId, 5000m, "EGP", PaymentMethod.Card, PaymentProviderType.Stripe,
            FixedNow, overpaymentPolicy: OverpaymentPolicy.AutoApplyToOldestInvoices);
        payment.AssignPaymentNumber("PAY-WH-1");
        payment.MarkProcessing("pi_wh_1", "processing", FixedNow);

        m.PayRepo.GetByProviderPaymentIdAsync(PaymentProviderType.Stripe, "pi_wh_1", Arg.Any<CancellationToken>()).Returns(payment);
        m.PayRepo.GetByIdAsync(payment.Id, orgId, Arg.Any<CancellationToken>()).Returns(payment);
        m.InvoiceRepo.GetOutstandingByResidentAsync(residentId, orgId, "EGP", Arg.Any<CancellationToken>()).Returns([inv]);

        m.ExternalProvider.VerifyAndParseWebhook(Arg.Any<string>(), Arg.Any<IReadOnlyDictionary<string, string>>(), FixedNow)
            .Returns(
                new ProviderWebhookVerificationResult(true, true, null, "evt_ok", "payment_intent.succeeded", FixedNow, "pi_wh_1", null, "ch_wh_1", null, 5000m, 145m, "EGP", "succeeded", null),
                new ProviderWebhookVerificationResult(true, true, null, "evt_late_fail", "payment_intent.payment_failed", FixedNow, "pi_wh_1", null, null, null, 5000m, null, "EGP", "failed", "Stale timeout"));

        var handler = new ProcessPaymentWebhookCommandHandler(
            m.ProviderFactory, m.WebhookRepo, m.PayRepo, m.IntentRepo, m.InvoiceRepo,
            m.LedgerRepo, m.JeNumGen, m.AuditRepo, m.SystemTenant, m.Uow, m.Clock);

        var okRes = await handler.Handle(
            new ProcessPaymentWebhookCommand(PaymentProviderType.Stripe, "{\"ok\":1}", new Dictionary<string, string>()),
            CancellationToken.None);

        Assert.True(okRes.IsSuccess);
        Assert.Equal(PaymentStatus.Succeeded, payment.Status);
        Assert.Equal(InvoiceStatus.Paid, inv.Status);
        Assert.Single(m.CapturedJes);

        var lateFailRes = await handler.Handle(
            new ProcessPaymentWebhookCommand(PaymentProviderType.Stripe, "{\"fail\":1}", new Dictionary<string, string>()),
            CancellationToken.None);

        Assert.True(lateFailRes.IsSuccess);
        Assert.Equal(PaymentStatus.Succeeded, payment.Status);
        Assert.Equal(2, m.CapturedWebhooks.Count);
        Assert.All(m.CapturedWebhooks, wh => Assert.Equal(orgId, wh.OrganizationId));
    }

    [Fact]
    public async Task RefundPayment_MultiplePartialToFull_OverRefund_AllocatedGuard_AndIdempotency()
    {
        var m = CreateMocks();
        var orgId = Guid.NewGuid();
        var payment = Payment.Create(orgId, Guid.NewGuid(), 10000m, "EGP", PaymentMethod.Card, PaymentProviderType.Stripe, FixedNow);
        payment.AssignPaymentNumber("PAY-REF-1");
        payment.AttachExternalIdentifiers("pi_ref_1", "cus_1", "ch_1", "succeeded", FixedNow);
        payment.MarkSucceeded(FixedNow, providerTransactionId: "ch_1", providerFee: 250m);
        payment.AllocateToInvoice(Guid.NewGuid(), 4000m, "EGP", FixedNow, Guid.NewGuid());

        m.PayRepo.GetByIdAsync(payment.Id, orgId, Arg.Any<CancellationToken>()).Returns(payment);
        m.ExternalProvider.RefundPaymentAsync(Arg.Any<ProviderRefundRequest>(), Arg.Any<CancellationToken>())
            .Returns(
                new ProviderRefundResult(true, true, "re_1", "succeeded"),
                new ProviderRefundResult(true, true, "re_2", "succeeded"));

        var handler = new RefundPaymentCommandHandler(
            m.PayRepo, m.LedgerRepo, m.JeNumGen, m.AuditRepo, m.ProviderFactory, m.Uow, m.Clock);

        var tooHigh = await handler.Handle(
            new RefundPaymentCommand(orgId, Guid.NewGuid(), payment.Id, 7000m, "Over limit"),
            CancellationToken.None);
        Assert.False(tooHigh.IsSuccess);
        Assert.Equal(ErrorType.Validation, tooHigh.Errors.First().Type);

        var ref1 = await handler.Handle(
            new RefundPaymentCommand(orgId, Guid.NewGuid(), payment.Id, 2500m, "Partial #1", IdempotencyKey: "ref-key-1"),
            CancellationToken.None);
        Assert.True(ref1.IsSuccess);
        Assert.Equal(PaymentStatus.PartiallyRefunded, payment.Status);
        Assert.Equal(2500m, payment.RefundedAmount);

        var ref1Retry = await handler.Handle(
            new RefundPaymentCommand(orgId, Guid.NewGuid(), payment.Id, 2500m, "Partial #1", IdempotencyKey: "ref-key-1"),
            CancellationToken.None);
        Assert.True(ref1Retry.IsSuccess);
        Assert.Equal(ref1.Value.Id, ref1Retry.Value.Id);

        payment.ReverseAllocation(payment.Allocations.Single().Id, "Reversing for full refund", FixedNow, Guid.NewGuid());
        var ref2 = await handler.Handle(
            new RefundPaymentCommand(orgId, Guid.NewGuid(), payment.Id, 7500m, "Remaining full refund", IdempotencyKey: "ref-key-2"),
            CancellationToken.None);

        Assert.True(ref2.IsSuccess);
        Assert.Equal(PaymentStatus.Refunded, payment.Status);
        Assert.Equal(10000m, payment.RefundedAmount);
        Assert.Equal(0m, payment.RemainingRefundableAmount);
        await m.ExternalProvider.Received(2).RefundPaymentAsync(Arg.Any<ProviderRefundRequest>(), Arg.Any<CancellationToken>());
        Assert.Equal(2, m.CapturedJes.Count);
    }

    [Fact]
    public async Task RecordProviderSettlement_AndReconcile_AndResolve_ShouldPreserveIntegrity()
    {
        var m = CreateMocks();
        var orgId = Guid.NewGuid();
        var payment1 = Payment.Create(orgId, Guid.NewGuid(), 10000m, "EGP", PaymentMethod.Card, PaymentProviderType.Stripe, FixedNow);
        payment1.AssignPaymentNumber("PAY-REC-1");
        payment1.AttachExternalIdentifiers("pi_rec_1", "cus_1", "tx_rec_1", "succeeded", FixedNow);
        payment1.MarkSucceeded(FixedNow, providerTransactionId: "tx_rec_1");

        var payment2 = Payment.Create(orgId, Guid.NewGuid(), 5000m, "EGP", PaymentMethod.Card, PaymentProviderType.Stripe, FixedNow);
        payment2.AssignPaymentNumber("PAY-REC-2");
        payment2.AttachExternalIdentifiers("pi_rec_2", "cus_1", "tx_rec_2", "succeeded", FixedNow);
        payment2.MarkSucceeded(FixedNow, providerTransactionId: "tx_rec_2", providerFee: 100m);

        m.PayRepo.GetByIdAsync(payment1.Id, orgId, Arg.Any<CancellationToken>()).Returns(payment1);

        var settleHandler = new RecordProviderSettlementCommandHandler(
            m.PayRepo, m.LedgerRepo, m.JeNumGen, m.AuditRepo, m.Uow, m.Clock);
        var settleRes = await settleHandler.Handle(
            new RecordProviderSettlementCommand(orgId, Guid.NewGuid(), payment1.Id, 300m, FixedNow),
            CancellationToken.None);

        Assert.True(settleRes.IsSuccess);
        Assert.Equal(9700m, settleRes.Value.NetSettlementAmount);
        var settleJe = Assert.Single(m.CapturedJes);
        Assert.Contains(settleJe.Lines, l => l.AccountCode == LedgerAccountCode.Cash && l.Debit == 9700m);
        Assert.Contains(settleJe.Lines, l => l.AccountCode == LedgerAccountCode.ProcessingFeeExpense && l.Debit == 300m);
        Assert.Contains(settleJe.Lines, l => l.AccountCode == LedgerAccountCode.PaymentProviderClearing && l.Credit == 10000m);

        m.PayRepo.GetByProviderAndWindowAsync(orgId, PaymentProviderType.Stripe, FixedNow.AddDays(-1), FixedNow.AddDays(1), Arg.Any<CancellationToken>())
            .Returns([payment1, payment2]);
        m.ExternalProvider.FetchSettlementReportAsync(FixedNow.AddDays(-1), FixedNow.AddDays(1), Arg.Any<CancellationToken>())
            .Returns(
            [
                new ProviderTransactionSnapshot("tx_rec_1", 10000m, 300m, 9700m, "EGP", "pi_rec_1", FixedNow),
                new ProviderTransactionSnapshot("tx_rec_2", 4800m, 100m, 4700m, "EGP", "pi_rec_2", FixedNow),
                new ProviderTransactionSnapshot("tx_extra_99", 1200m, 35m, 1165m, "EGP", "pi_extra", FixedNow)
            ]);

        var reconcileHandler = new ReconcilePaymentsCommandHandler(
            m.PayRepo, m.ReconRepo, m.ProviderFactory, m.AuditRepo, m.Uow, m.Clock);
        var batchRes = await reconcileHandler.Handle(
            new ReconcilePaymentsCommand(orgId, Guid.NewGuid(), PaymentProviderType.Stripe, FixedNow.AddDays(-1), FixedNow.AddDays(1)),
            CancellationToken.None);

        Assert.True(batchRes.IsSuccess);
        Assert.Equal(3, batchRes.Value.TotalChecked);
        Assert.Equal(1, batchRes.Value.MatchedCount);
        Assert.Equal(2, batchRes.Value.ExceptionCount);
        Assert.Equal(5000m, payment2.Amount);

        var mismatchDomainRecord = m.CapturedRecons.First(r => r.Status == ReconciliationStatus.AmountMismatch);
        m.ReconRepo.GetByIdAsync(mismatchDomainRecord.Id, orgId, Arg.Any<CancellationToken>()).Returns(mismatchDomainRecord);

        var resolveHandler = new ResolveReconciliationRecordCommandHandler(m.ReconRepo, m.AuditRepo, m.Uow, m.Clock);
        var resolveRes = await resolveHandler.Handle(
            new ResolveReconciliationRecordCommand(orgId, Guid.NewGuid(), mismatchDomainRecord.Id, "Confirmed partial chargeback fee with Stripe"),
            CancellationToken.None);

        Assert.True(resolveRes.IsSuccess);
        Assert.Equal(ReconciliationStatus.Resolved, resolveRes.Value.Status);
    }

    [Fact]
    public void Validators_ShouldRejectInvalidInputs()
    {
        var refundValidator = new RefundPaymentCommandValidator();
        Assert.False(refundValidator.Validate(new RefundPaymentCommand(Guid.Empty, Guid.NewGuid(), Guid.NewGuid(), 0m, "")).IsValid);

        var webhookValidator = new ProcessPaymentWebhookCommandValidator();
        Assert.False(webhookValidator.Validate(new ProcessPaymentWebhookCommand(PaymentProviderType.Stripe, "", new Dictionary<string, string>())).IsValid);
    }

    [Fact]
    public async Task Queries_ShouldPopulatePagedListParametersInCorrectOrder_AndRefundShouldForwardProviderTransactionId()
    {
        var m = CreateMocks();
        var orgId = Guid.NewGuid();
        var payment = Payment.Create(orgId, Guid.NewGuid(), 1500m, "EGP", PaymentMethod.Card, PaymentProviderType.Paymob, FixedNow);
        payment.AttachExternalIdentifiers("order_8877", null, "tx_4455", "succeeded", FixedNow);
        payment.MarkSucceeded(FixedNow, providerTransactionId: "tx_4455");

        m.PayRepo.GetPagedAsync(Arg.Any<PaymentFilter>(), Arg.Any<CancellationToken>())
            .Returns(([payment], 12));
        var payQueryHandler = new GetPaymentsQueryHandler(m.PayRepo);
        var pagedPay = await payQueryHandler.Handle(new GetPaymentsQuery(orgId, PageNumber: 2, PageSize: 5), CancellationToken.None);
        Assert.True(pagedPay.IsSuccess);
        Assert.Equal(2, pagedPay.Value.PageNumber);
        Assert.Equal(5, pagedPay.Value.PageSize);
        Assert.Equal(12, pagedPay.Value.TotalCount);
        Assert.Equal(3, pagedPay.Value.TotalPages);

        m.LedgerRepo.GetPagedAsync(Arg.Any<LedgerFilter>(), Arg.Any<CancellationToken>())
            .Returns(([], 25));
        var ledgerQueryHandler = new GetLedgerEntriesQueryHandler(m.LedgerRepo);
        var pagedJe = await ledgerQueryHandler.Handle(new GetLedgerEntriesQuery(orgId, PageNumber: 3, PageSize: 10), CancellationToken.None);
        Assert.Equal(3, pagedJe.Value.PageNumber);
        Assert.Equal(10, pagedJe.Value.PageSize);
        Assert.Equal(25, pagedJe.Value.TotalCount);

        m.PayRepo.GetByIdAsync(payment.Id, orgId, Arg.Any<CancellationToken>()).Returns(payment);
        m.ExternalProvider.RefundPaymentAsync(Arg.Any<ProviderRefundRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ProviderRefundResult(true, true, "pm_ref_1", "refunded"));
        var refundHandler = new RefundPaymentCommandHandler(
            m.PayRepo, m.LedgerRepo, m.JeNumGen, m.AuditRepo, m.ProviderFactory, m.Uow, m.Clock);
        var refundRes = await refundHandler.Handle(
            new RefundPaymentCommand(orgId, Guid.NewGuid(), payment.Id, 500m, "Paymob refund", "pm-ref-1"),
            CancellationToken.None);
        Assert.True(refundRes.IsSuccess);
        await m.ExternalProvider.Received(1).RefundPaymentAsync(
            Arg.Is<ProviderRefundRequest>(r => r.ProviderPaymentId == "order_8877" && r.ProviderTransactionId == "tx_4455"),
            Arg.Any<CancellationToken>());
    }
}
