using System.Data;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using PropertyManagement.Application.Abstractions;
using PropertyManagement.Application.Abstractions.Audit;
using PropertyManagement.Application.Abstractions.Billing;
using PropertyManagement.Application.Abstractions.Ledger;
using PropertyManagement.Application.Abstractions.Organizations;
using PropertyManagement.Application.Abstractions.Payments;
using PropertyManagement.Application.Abstractions.Properties;
using PropertyManagement.Application.Abstractions.Repositories;
using PropertyManagement.Application.Abstractions.Residents;
using PropertyManagement.Application.Common;
using PropertyManagement.Application.Common.Enums;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Application.Features.Payments.Allocations;
using PropertyManagement.Application.Features.Payments.Intents;
using PropertyManagement.Application.Features.Payments.Lifecycle;
using PropertyManagement.Application.Features.Payments.Refunds;
using PropertyManagement.Application.Features.Payments.Responses;
using PropertyManagement.Domain.Entities.Audit;
using PropertyManagement.Domain.Entities.Billing;
using PropertyManagement.Domain.Entities.Ledger;
using PropertyManagement.Domain.Entities.Payments;
using PropertyManagement.Domain.Entities.Payments.Services;
using PropertyManagement.Domain.Entities.Residents;
using PropertyManagement.Domain.Enums;
using AppDbTransaction = PropertyManagement.Application.Abstractions.IDbTransaction;

namespace PropertyManagement.Application.Tests.Payments;

public sealed class PaymentWorkflowTests
{
    private static readonly DateTime FixedNow = new(2026, 6, 15, 10, 0, 0, DateTimeKind.Utc);

    private static Resident MakeResident(Guid orgId) =>
        Resident.Create(orgId, "Tarek", "Mansour", "01001234567", FixedNow);

    private static Invoice MakeIssuedInvoice(
        Guid orgId,
        Guid residentId,
        decimal unitPrice,
        DateOnly issueDate,
        DateOnly dueDate,
        string invoiceNo,
        string currency = "EGP",
        bool markOverdue = false)
    {
        var inv = Invoice.CreateDraft(
            orgId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            residentId,
            BillingPeriod.ForMonth(dueDate.Year, dueDate.Month),
            issueDate,
            dueDate,
            currency,
            FixedNow);
        inv.AddLine(ChargeType.Rent, "Monthly Rent", 1m, unitPrice, FixedNow);
        inv.Issue(invoiceNo, issueDate, FixedNow);
        if (markOverdue)
        {
            inv.MarkOverdue(dueDate.AddDays(5), FixedNow);
        }
        return inv;
    }

    private static (
        IPaymentRepository PayRepo,
        IPaymentIntentRepository IntentRepo,
        IPaymentNumberGenerator PayNumGen,
        IResidentRepository ResidentRepo,
        ILeaseRepository LeaseRepo,
        IInvoiceRepository InvoiceRepo,
        ILedgerRepository LedgerRepo,
        IJournalEntryNumberGenerator JeNumGen,
        IFinancialAuditLogRepository AuditRepo,
        IPaymentProviderFactory ProviderFactory,
        IPaymentProvider ExternalProvider,
        IUnitOfWork Uow,
        IClock Clock,
        List<JournalEntry> CapturedJournalEntries,
        List<FinancialAuditLog> CapturedAudits) CreateMocks()
    {
        var payRepo = Substitute.For<IPaymentRepository>();
        var intentRepo = Substitute.For<IPaymentIntentRepository>();
        var payNumGen = Substitute.For<IPaymentNumberGenerator>();
        payNumGen.GenerateNextAsync(Arg.Any<Guid>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns("PAY-2026-000001");

        var residentRepo = Substitute.For<IResidentRepository>();
        var leaseRepo = Substitute.For<ILeaseRepository>();
        var invoiceRepo = Substitute.For<IInvoiceRepository>();
        var ledgerRepo = Substitute.For<ILedgerRepository>();
        var jeNumGen = Substitute.For<IJournalEntryNumberGenerator>();
        jeNumGen.GenerateNextAsync(Arg.Any<Guid>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns("JE-2026-000001", "JE-2026-000002", "JE-2026-000003", "JE-2026-000004");

        var auditRepo = Substitute.For<IFinancialAuditLogRepository>();
        var providerFactory = Substitute.For<IPaymentProviderFactory>();
        var externalProvider = Substitute.For<IPaymentProvider>();
        providerFactory.Resolve(Arg.Any<PaymentProviderType>()).Returns(externalProvider);

        var tx = Substitute.For<AppDbTransaction>();
        var uow = Substitute.For<IUnitOfWork>();
        uow.BeginTransactionAsync(IsolationLevel.Serializable, Arg.Any<CancellationToken>()).Returns(tx);

        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(FixedNow);

        var capturedJes = new List<JournalEntry>();
        ledgerRepo.When(x => x.AddAsync(Arg.Any<JournalEntry>(), Arg.Any<CancellationToken>()))
            .Do(c => capturedJes.Add(c.Arg<JournalEntry>()));

        var capturedAudits = new List<FinancialAuditLog>();
        auditRepo.When(x => x.AddAsync(Arg.Any<FinancialAuditLog>(), Arg.Any<CancellationToken>()))
            .Do(c => capturedAudits.Add(c.Arg<FinancialAuditLog>()));

        return (payRepo, intentRepo, payNumGen, residentRepo, leaseRepo, invoiceRepo, ledgerRepo, jeNumGen, auditRepo, providerFactory, externalProvider, uow, clock, capturedJes, capturedAudits);
    }

    [Fact]
    public void AddApplication_ShouldRegisterAllPaymentAndLedgerHandlersAndValidators()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();

        services.AddScoped(_ => Substitute.For<IPaymentRepository>());
        services.AddScoped(_ => Substitute.For<IPaymentIntentRepository>());
        services.AddScoped(_ => Substitute.For<IPaymentNumberGenerator>());
        services.AddScoped(_ => Substitute.For<IWebhookEventRepository>());
        services.AddScoped(_ => Substitute.For<IPaymentReconciliationRepository>());
        services.AddScoped(_ => Substitute.For<ILedgerRepository>());
        services.AddScoped(_ => Substitute.For<IJournalEntryNumberGenerator>());
        services.AddScoped(_ => Substitute.For<IFinancialAuditLogRepository>());
        services.AddScoped(_ => Substitute.For<IPaymentProviderFactory>());
        services.AddScoped(_ => Substitute.For<IResidentRepository>());
        services.AddScoped(_ => Substitute.For<ILeaseRepository>());
        services.AddScoped(_ => Substitute.For<IInvoiceRepository>());
        services.AddScoped(_ => Substitute.For<IChargeRepository>());
        services.AddScoped(_ => Substitute.For<IInvoiceNumberGenerator>());
        services.AddScoped(_ => Substitute.For<IOrganizationRepository>());
        services.AddScoped(_ => Substitute.For<IPropertyRepository>());
        services.AddScoped(_ => Substitute.For<IUnitRepository>());
        services.AddScoped(_ => Substitute.For<IUnitOfWork>());
        services.AddScoped(_ => Substitute.For<IClock>());

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetService<ISender>());
        Assert.NotNull(scope.ServiceProvider.GetService<IRequestHandler<CreatePaymentCommand, Result<PaymentResponse>>>());
        Assert.NotNull(scope.ServiceProvider.GetService<IRequestHandler<CreatePaymentIntentCommand, Result<PaymentIntentResponse>>>());
        Assert.NotNull(scope.ServiceProvider.GetService<IRequestHandler<RefundPaymentCommand, Result<RefundResponse>>>());
    }

    [Fact]
    public async Task CreatePaymentIntent_ShouldCreatePendingIntent_WithExternalProviderSession()
    {
        var mocks = CreateMocks();
        var orgId = Guid.NewGuid();
        var resident = MakeResident(orgId);
        mocks.ResidentRepo.GetByIdAsync(resident.Id, orgId, Arg.Any<CancellationToken>()).Returns(resident);
        mocks.ExternalProvider.CreateIntentAsync(Arg.Any<ProviderCreateIntentRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ProviderCreateIntentResult(true, "pi_stripe_99", "cs_99", "https://checkout.local/99", true));

        var handler = new CreatePaymentIntentCommandHandler(
            mocks.IntentRepo, mocks.ResidentRepo, mocks.LeaseRepo, mocks.InvoiceRepo,
            mocks.ProviderFactory, mocks.AuditRepo, mocks.Uow, mocks.Clock);

        var res = await handler.Handle(
            new CreatePaymentIntentCommand(orgId, Guid.NewGuid(), resident.Id, 10000m, "egp", PaymentMethod.Card, PaymentProviderType.Stripe, IdempotencyKey: "intent-key-1"),
            CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal(PaymentIntentStatus.RequiresAction, res.Value.Status);
        Assert.Equal("pi_stripe_99", res.Value.ProviderIntentId);
        Assert.Equal("EGP", res.Value.Currency);
        Assert.Single(mocks.CapturedAudits);
    }

    [Fact]
    public async Task CreatePayment_DuplicateRetryWithSameIdempotencyKey_ShouldReturnExistingWithoutSecondProviderCall()
    {
        var mocks = CreateMocks();
        var orgId = Guid.NewGuid();
        var existingPayment = Payment.Create(
            orgId, Guid.NewGuid(), 5000m, "EGP", PaymentMethod.Card, PaymentProviderType.Stripe,
            FixedNow, idempotencyKey: "idem-retry-1");
        mocks.PayRepo.GetByIdempotencyKeyAsync("idem-retry-1", orgId, Arg.Any<CancellationToken>())
            .Returns(existingPayment);

        var handler = new CreatePaymentCommandHandler(
            mocks.PayRepo, mocks.IntentRepo, mocks.PayNumGen, mocks.ResidentRepo, mocks.LeaseRepo,
            mocks.InvoiceRepo, mocks.LedgerRepo, mocks.JeNumGen, mocks.AuditRepo,
            mocks.ProviderFactory, mocks.Uow, mocks.Clock);

        var res = await handler.Handle(
            new CreatePaymentCommand(orgId, Guid.NewGuid(), existingPayment.ResidentId, 5000m, "EGP",
                PaymentMethod.Card, PaymentProviderType.Stripe, IdempotencyKey: "idem-retry-1"),
            CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal(existingPayment.Id, res.Value.Id);
        await mocks.ExternalProvider.DidNotReceive().CreateOrCapturePaymentAsync(Arg.Any<ProviderPaymentRequest>(), Arg.Any<CancellationToken>());
        await mocks.PayRepo.DidNotReceive().AddAsync(Arg.Any<Payment>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreatePayment_PartialPayment_ThenSecondPayment_ShouldMakeInvoicePartiallyPaidThenPaid()
    {
        var mocks = CreateMocks();
        var orgId = Guid.NewGuid();
        var resident = MakeResident(orgId);
        var inv = MakeIssuedInvoice(orgId, resident.Id, 10000m, new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 5), "INV-2026-000001");

        mocks.ResidentRepo.GetByIdAsync(resident.Id, orgId, Arg.Any<CancellationToken>()).Returns(resident);
        mocks.InvoiceRepo.GetByIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), orgId, Arg.Any<CancellationToken>())
            .Returns([inv]);

        var handler = new CreatePaymentCommandHandler(
            mocks.PayRepo, mocks.IntentRepo, mocks.PayNumGen, mocks.ResidentRepo, mocks.LeaseRepo,
            mocks.InvoiceRepo, mocks.LedgerRepo, mocks.JeNumGen, mocks.AuditRepo,
            mocks.ProviderFactory, mocks.Uow, mocks.Clock);

        var first = await handler.Handle(
            new CreatePaymentCommand(
                orgId, Guid.NewGuid(), resident.Id, 4000m, "EGP",
                PaymentMethod.Cash, PaymentProviderType.Manual,
                Allocations: [new RequestedInvoiceAllocation(inv.Id, 4000m)]),
            CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.Equal(PaymentStatus.Succeeded, first.Value.Status);
        Assert.Equal(4000m, first.Value.AllocatedAmount);
        Assert.Equal(0m, first.Value.UnappliedAmount);
        Assert.Equal(InvoiceStatus.PartiallyPaid, inv.Status);
        Assert.Equal(6000m, inv.BalanceDue);

        var second = await handler.Handle(
            new CreatePaymentCommand(
                orgId, Guid.NewGuid(), resident.Id, 6000m, "EGP",
                PaymentMethod.Cash, PaymentProviderType.Manual,
                Allocations: [new RequestedInvoiceAllocation(inv.Id, 6000m)]),
            CancellationToken.None);

        Assert.True(second.IsSuccess);
        Assert.Equal(InvoiceStatus.Paid, inv.Status);
        Assert.Equal(0m, inv.BalanceDue);
        Assert.Equal(2, mocks.CapturedJournalEntries.Count);
        Assert.All(mocks.CapturedJournalEntries, je =>
        {
            Assert.Equal(je.TotalDebits, je.TotalCredits);
            Assert.Contains(je.Lines, l => l.AccountCode == LedgerAccountCode.Cash && l.Debit > 0);
            Assert.Contains(je.Lines, l => l.AccountCode == LedgerAccountCode.AccountsReceivable && l.Credit > 0);
        });
    }

    [Fact]
    public async Task CreatePayment_OnePaymentAcrossMultipleInvoices_ShouldAllocateBothAndBalanceLedger()
    {
        var mocks = CreateMocks();
        var orgId = Guid.NewGuid();
        var resident = MakeResident(orgId);
        var invA = MakeIssuedInvoice(orgId, resident.Id, 5000m, new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 5), "INV-1");
        var invB = MakeIssuedInvoice(orgId, resident.Id, 7000m, new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 5), "INV-2");

        mocks.ResidentRepo.GetByIdAsync(resident.Id, orgId, Arg.Any<CancellationToken>()).Returns(resident);
        mocks.InvoiceRepo.GetByIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), orgId, Arg.Any<CancellationToken>())
            .Returns([invA, invB]);

        var handler = new CreatePaymentCommandHandler(
            mocks.PayRepo, mocks.IntentRepo, mocks.PayNumGen, mocks.ResidentRepo, mocks.LeaseRepo,
            mocks.InvoiceRepo, mocks.LedgerRepo, mocks.JeNumGen, mocks.AuditRepo,
            mocks.ProviderFactory, mocks.Uow, mocks.Clock);

        var res = await handler.Handle(
            new CreatePaymentCommand(
                orgId, Guid.NewGuid(), resident.Id, 12000m, "EGP",
                PaymentMethod.BankTransfer, PaymentProviderType.Manual,
                ConfirmImmediately: true,
                Allocations: [
                    new RequestedInvoiceAllocation(invA.Id, 5000m),
                    new RequestedInvoiceAllocation(invB.Id, 7000m)
                ]),
            CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal(2, res.Value.Allocations.Count);
        Assert.Equal(12000m, res.Value.AllocatedAmount);
        Assert.Equal(0m, res.Value.UnappliedAmount);
        Assert.Equal(InvoiceStatus.Paid, invA.Status);
        Assert.Equal(InvoiceStatus.Paid, invB.Status);
    }

    [Fact]
    public async Task CreatePayment_OverpaymentPolicies_ShouldHandleKeepUnapplied_AutoApply_AndReject()
    {
        var mocks1 = CreateMocks();
        var orgId = Guid.NewGuid();
        var resident = MakeResident(orgId);
        var inv1 = MakeIssuedInvoice(orgId, resident.Id, 10000m, new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 5), "INV-K1");
        mocks1.ResidentRepo.GetByIdAsync(resident.Id, orgId, Arg.Any<CancellationToken>()).Returns(resident);
        mocks1.InvoiceRepo.GetByIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), orgId, Arg.Any<CancellationToken>()).Returns([inv1]);

        var handler1 = new CreatePaymentCommandHandler(
            mocks1.PayRepo, mocks1.IntentRepo, mocks1.PayNumGen, mocks1.ResidentRepo, mocks1.LeaseRepo,
            mocks1.InvoiceRepo, mocks1.LedgerRepo, mocks1.JeNumGen, mocks1.AuditRepo,
            mocks1.ProviderFactory, mocks1.Uow, mocks1.Clock);

        var keepRes = await handler1.Handle(
            new CreatePaymentCommand(
                orgId, Guid.NewGuid(), resident.Id, 12000m, "EGP",
                PaymentMethod.Cash, PaymentProviderType.Manual,
                OverpaymentPolicy: OverpaymentPolicy.KeepUnappliedCredit,
                Allocations: [new RequestedInvoiceAllocation(inv1.Id, 10000m)]),
            CancellationToken.None);

        Assert.True(keepRes.IsSuccess);
        Assert.Equal(10000m, keepRes.Value.AllocatedAmount);
        Assert.Equal(2000m, keepRes.Value.UnappliedAmount);
        var je1 = Assert.Single(mocks1.CapturedJournalEntries);
        Assert.Equal(12000m, je1.TotalDebits);
        Assert.Equal(10000m, je1.Lines.Where(l => l.AccountCode == LedgerAccountCode.AccountsReceivable).Sum(l => l.Credit));
        Assert.Equal(2000m, je1.Lines.Where(l => l.AccountCode == LedgerAccountCode.CustomerUnappliedCredit).Sum(l => l.Credit));

        var mocks2 = CreateMocks();
        var invOld = MakeIssuedInvoice(orgId, resident.Id, 4000m, new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 5), "INV-OLD", markOverdue: true);
        var invNew = MakeIssuedInvoice(orgId, resident.Id, 6000m, new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 20), "INV-NEW");
        mocks2.ResidentRepo.GetByIdAsync(resident.Id, orgId, Arg.Any<CancellationToken>()).Returns(resident);
        mocks2.InvoiceRepo.GetOutstandingByResidentAsync(resident.Id, orgId, "EGP", Arg.Any<CancellationToken>())
            .Returns([invNew, invOld]);

        var handler2 = new CreatePaymentCommandHandler(
            mocks2.PayRepo, mocks2.IntentRepo, mocks2.PayNumGen, mocks2.ResidentRepo, mocks2.LeaseRepo,
            mocks2.InvoiceRepo, mocks2.LedgerRepo, mocks2.JeNumGen, mocks2.AuditRepo,
            mocks2.ProviderFactory, mocks2.Uow, mocks2.Clock);

        var autoRes = await handler2.Handle(
            new CreatePaymentCommand(
                orgId, Guid.NewGuid(), resident.Id, 9000m, "EGP",
                PaymentMethod.Cash, PaymentProviderType.Manual,
                OverpaymentPolicy: OverpaymentPolicy.AutoApplyToOldestInvoices,
                AllocationStrategy: AllocationStrategy.OldestOverdueFirst),
            CancellationToken.None);

        Assert.True(autoRes.IsSuccess);
        Assert.Equal(InvoiceStatus.Paid, invOld.Status);
        Assert.Equal(InvoiceStatus.PartiallyPaid, invNew.Status);
        Assert.Equal(1000m, invNew.BalanceDue);

        var mocks3 = CreateMocks();
        var invSmall = MakeIssuedInvoice(orgId, resident.Id, 5000m, new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 5), "INV-SMALL");
        mocks3.ResidentRepo.GetByIdAsync(resident.Id, orgId, Arg.Any<CancellationToken>()).Returns(resident);
        mocks3.InvoiceRepo.GetOutstandingByResidentAsync(resident.Id, orgId, "EGP", Arg.Any<CancellationToken>())
            .Returns([invSmall]);

        var handler3 = new CreatePaymentCommandHandler(
            mocks3.PayRepo, mocks3.IntentRepo, mocks3.PayNumGen, mocks3.ResidentRepo, mocks3.LeaseRepo,
            mocks3.InvoiceRepo, mocks3.LedgerRepo, mocks3.JeNumGen, mocks3.AuditRepo,
            mocks3.ProviderFactory, mocks3.Uow, mocks3.Clock);

        var rejectRes = await handler3.Handle(
            new CreatePaymentCommand(
                orgId, Guid.NewGuid(), resident.Id, 6000m, "EGP",
                PaymentMethod.Cash, PaymentProviderType.Manual,
                OverpaymentPolicy: OverpaymentPolicy.RejectOverpayment,
                AllocationStrategy: AllocationStrategy.OldestDueFirst),
            CancellationToken.None);

        Assert.False(rejectRes.IsSuccess);
        Assert.Equal(ErrorType.Validation, rejectRes.Errors.First().Type);
    }

    [Fact]
    public async Task CreatePayment_ExternalProvider_ShouldDebitPaymentProviderClearingAccount()
    {
        var mocks = CreateMocks();
        var orgId = Guid.NewGuid();
        var resident = MakeResident(orgId);
        var inv = MakeIssuedInvoice(orgId, resident.Id, 10000m, new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 5), "INV-STR");

        mocks.ResidentRepo.GetByIdAsync(resident.Id, orgId, Arg.Any<CancellationToken>()).Returns(resident);
        mocks.InvoiceRepo.GetByIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), orgId, Arg.Any<CancellationToken>()).Returns([inv]);
        mocks.ExternalProvider.CreateOrCapturePaymentAsync(Arg.Any<ProviderPaymentRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ProviderPaymentResult(true, true, false, "pi_1", "cus_1", "tx_1", "succeeded", 250m));

        var handler = new CreatePaymentCommandHandler(
            mocks.PayRepo, mocks.IntentRepo, mocks.PayNumGen, mocks.ResidentRepo, mocks.LeaseRepo,
            mocks.InvoiceRepo, mocks.LedgerRepo, mocks.JeNumGen, mocks.AuditRepo,
            mocks.ProviderFactory, mocks.Uow, mocks.Clock);

        var res = await handler.Handle(
            new CreatePaymentCommand(
                orgId, Guid.NewGuid(), resident.Id, 10000m, "EGP",
                PaymentMethod.Card, PaymentProviderType.Stripe,
                Allocations: [new RequestedInvoiceAllocation(inv.Id, 10000m)]),
            CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal(250m, res.Value.ProviderFee);
        Assert.Equal(9750m, res.Value.NetSettlementAmount);
        var je = Assert.Single(mocks.CapturedJournalEntries);
        Assert.Contains(je.Lines, l => l.AccountCode == LedgerAccountCode.PaymentProviderClearing && l.Debit == 10000m);
        Assert.Contains(je.Lines, l => l.AccountCode == LedgerAccountCode.AccountsReceivable && l.Credit == 10000m);
    }

    [Fact]
    public async Task CreatePayment_CurrencyMismatchAndCrossTenantResident_ShouldFailSafely()
    {
        var mocks = CreateMocks();
        var orgId = Guid.NewGuid();
        var otherOrgId = Guid.NewGuid();
        var foreignResident = MakeResident(otherOrgId);
        mocks.ResidentRepo.GetByIdAsync(foreignResident.Id, orgId, Arg.Any<CancellationToken>()).Returns((Resident?)null);

        var handler = new CreatePaymentCommandHandler(
            mocks.PayRepo, mocks.IntentRepo, mocks.PayNumGen, mocks.ResidentRepo, mocks.LeaseRepo,
            mocks.InvoiceRepo, mocks.LedgerRepo, mocks.JeNumGen, mocks.AuditRepo,
            mocks.ProviderFactory, mocks.Uow, mocks.Clock);

        var crossTenantRes = await handler.Handle(
            new CreatePaymentCommand(orgId, Guid.NewGuid(), foreignResident.Id, 1000m, "EGP",
                PaymentMethod.Cash, PaymentProviderType.Manual),
            CancellationToken.None);

        Assert.False(crossTenantRes.IsSuccess);
        Assert.Equal(ErrorType.NotFound, crossTenantRes.Errors.First().Type);

        var resident = MakeResident(orgId);
        var usdInvoice = MakeIssuedInvoice(orgId, resident.Id, 500m, new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 5), "INV-USD", currency: "USD");
        mocks.ResidentRepo.GetByIdAsync(resident.Id, orgId, Arg.Any<CancellationToken>()).Returns(resident);
        mocks.InvoiceRepo.GetByIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), orgId, Arg.Any<CancellationToken>()).Returns([usdInvoice]);

        var mismatchRes = await handler.Handle(
            new CreatePaymentCommand(orgId, Guid.NewGuid(), resident.Id, 500m, "EGP",
                PaymentMethod.Cash, PaymentProviderType.Manual,
                Allocations: [new RequestedInvoiceAllocation(usdInvoice.Id, 500m)]),
            CancellationToken.None);

        Assert.False(mismatchRes.IsSuccess);
        Assert.Equal(ErrorType.Validation, mismatchRes.Errors.First().Type);
    }

    [Fact]
    public async Task AllocateAndReverseAllocation_ShouldSyncInvoiceAndPostReversalJournalEntry()
    {
        var mocks = CreateMocks();
        var orgId = Guid.NewGuid();
        var resident = MakeResident(orgId);
        var inv = MakeIssuedInvoice(orgId, resident.Id, 5000m, new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 5), "INV-REV");

        var payment = Payment.Create(orgId, resident.Id, 5000m, "EGP", PaymentMethod.Cash, PaymentProviderType.Manual, FixedNow);
        payment.AssignPaymentNumber("PAY-2026-000010");
        payment.MarkSucceeded(FixedNow, confirmedByUserId: Guid.NewGuid());

        mocks.PayRepo.GetByIdAsync(payment.Id, orgId, Arg.Any<CancellationToken>()).Returns(payment);
        mocks.InvoiceRepo.GetByIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), orgId, Arg.Any<CancellationToken>()).Returns([inv]);
        mocks.InvoiceRepo.GetByIdAsync(inv.Id, orgId, Arg.Any<CancellationToken>()).Returns(inv);
        mocks.LedgerRepo.ExistsByIdempotencyKeyAsync(orgId, "PaymentSucceeded", payment.Id, $"receipt:{payment.Id}", Arg.Any<CancellationToken>())
            .Returns(false);

        var allocateHandler = new AllocatePaymentCommandHandler(
            mocks.PayRepo, mocks.InvoiceRepo, mocks.LedgerRepo, mocks.JeNumGen, mocks.AuditRepo, mocks.Uow, mocks.Clock);

        var allocRes = await allocateHandler.Handle(
            new AllocatePaymentCommand(orgId, Guid.NewGuid(), payment.Id, AllocationStrategy.Explicit, [new RequestedInvoiceAllocation(inv.Id, 5000m)]),
            CancellationToken.None);

        Assert.True(allocRes.IsSuccess);
        Assert.Equal(InvoiceStatus.Paid, inv.Status);
        var allocId = Assert.Single(allocRes.Value.Allocations).Id;

        var reverseHandler = new ReversePaymentAllocationCommandHandler(
            mocks.PayRepo, mocks.InvoiceRepo, mocks.LedgerRepo, mocks.JeNumGen, mocks.AuditRepo, mocks.Uow, mocks.Clock);

        var revRes = await reverseHandler.Handle(
            new ReversePaymentAllocationCommand(orgId, Guid.NewGuid(), payment.Id, allocId, "Applied to wrong invoice"),
            CancellationToken.None);

        Assert.True(revRes.IsSuccess);
        Assert.Equal(0m, revRes.Value.AllocatedAmount);
        Assert.Equal(5000m, revRes.Value.UnappliedAmount);
        Assert.Equal(InvoiceStatus.Overdue, inv.Status);
        Assert.Equal(5000m, inv.BalanceDue);
        Assert.Equal(2, mocks.CapturedJournalEntries.Count);
        var reversalJe = mocks.CapturedJournalEntries[1];
        Assert.Contains(reversalJe.Lines, l => l.AccountCode == LedgerAccountCode.AccountsReceivable && l.Debit == 5000m);
        Assert.Contains(reversalJe.Lines, l => l.AccountCode == LedgerAccountCode.CustomerUnappliedCredit && l.Credit == 5000m);
    }

    [Fact]
    public async Task ConfirmAndCancel_ShouldEnforceStateMachineAndRowVersion()
    {
        var mocks = CreateMocks();
        var orgId = Guid.NewGuid();
        var payment = Payment.Create(orgId, Guid.NewGuid(), 3000m, "EGP", PaymentMethod.BankTransfer, PaymentProviderType.Manual, FixedNow);
        mocks.PayRepo.GetByIdAsync(payment.Id, orgId, Arg.Any<CancellationToken>()).Returns(payment);

        var cancelHandler = new CancelPaymentCommandHandler(mocks.PayRepo, mocks.AuditRepo, mocks.Uow, mocks.Clock);
        var invalidRowRes = await cancelHandler.Handle(
            new CancelPaymentCommand(orgId, Guid.NewGuid(), payment.Id, "Duplicate", RowVersion: "not-valid-base64!!!"),
            CancellationToken.None);
        Assert.False(invalidRowRes.IsSuccess);
        Assert.Equal(ErrorType.Validation, invalidRowRes.Errors.First().Type);

        var cancelOk = await cancelHandler.Handle(
            new CancelPaymentCommand(orgId, Guid.NewGuid(), payment.Id, "Tenant cancelled transfer"),
            CancellationToken.None);
        Assert.True(cancelOk.IsSuccess);
        Assert.Equal(PaymentStatus.Cancelled, cancelOk.Value.Status);

        var confirmHandler = new ConfirmPaymentCommandHandler(
            mocks.PayRepo, mocks.InvoiceRepo, mocks.LedgerRepo, mocks.JeNumGen, mocks.AuditRepo, mocks.Uow, mocks.Clock);
        var confirmCancelled = await confirmHandler.Handle(
            new ConfirmPaymentCommand(orgId, Guid.NewGuid(), payment.Id),
            CancellationToken.None);
        Assert.False(confirmCancelled.IsSuccess);
        Assert.Equal(ErrorType.Conflict, confirmCancelled.Errors.First().Type);
    }

    [Fact]
    public async Task SyncPaymentStatus_PollingFallback_ShouldTransitionProcessingToSucceededAndAllocate()
    {
        var mocks = CreateMocks();
        var orgId = Guid.NewGuid();
        var resident = MakeResident(orgId);
        var inv = MakeIssuedInvoice(orgId, resident.Id, 4500m, new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 5), "INV-POLL");

        var payment = Payment.Create(orgId, resident.Id, 4500m, "EGP", PaymentMethod.Card, PaymentProviderType.Stripe, FixedNow);
        payment.AssignPaymentNumber("PAY-2026-000022");
        payment.MarkProcessing("pi_poll_1", "processing", FixedNow);

        mocks.PayRepo.GetByIdAsync(payment.Id, orgId, Arg.Any<CancellationToken>()).Returns(payment);
        mocks.InvoiceRepo.GetOutstandingByResidentAsync(resident.Id, orgId, "EGP", Arg.Any<CancellationToken>()).Returns([inv]);
        mocks.ExternalProvider.GetPaymentStatusAsync("pi_poll_1", Arg.Any<CancellationToken>())
            .Returns(new ProviderPaymentResult(true, true, false, "pi_poll_1", "cus_1", "ch_poll_1", "succeeded", 120m));

        var handler = new SyncPaymentStatusCommandHandler(
            mocks.PayRepo, mocks.InvoiceRepo, mocks.LedgerRepo, mocks.JeNumGen,
            mocks.AuditRepo, mocks.ProviderFactory, mocks.Uow, mocks.Clock);

        var res = await handler.Handle(new SyncPaymentStatusCommand(orgId, Guid.NewGuid(), payment.Id), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.Equal(PaymentStatus.Succeeded, res.Value.Status);
        Assert.Equal(120m, res.Value.ProviderFee);
        Assert.Equal(4500m, res.Value.AllocatedAmount);
        Assert.Equal(InvoiceStatus.Paid, inv.Status);
        Assert.Single(mocks.CapturedJournalEntries);
    }
}
