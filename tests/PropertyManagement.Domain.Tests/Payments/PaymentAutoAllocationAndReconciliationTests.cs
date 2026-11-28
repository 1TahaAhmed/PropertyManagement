using PropertyManagement.Domain.Entities.Billing;
using PropertyManagement.Domain.Entities.Payments;
using PropertyManagement.Domain.Entities.Payments.Services;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Domain.Tests.Payments;

public sealed class PaymentAutoAllocationAndReconciliationTests
{
    private static readonly DateTime Now = new(2026, 4, 10, 12, 0, 0, DateTimeKind.Utc);

    private static Invoice CreateIssuedInvoice(Guid orgId, Guid residentId, decimal amount, DateOnly dueDate, string number)
    {
        var inv = Invoice.CreateDraft(
            orgId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            residentId,
            BillingPeriod.ForMonth(dueDate.Year, dueDate.Month),
            dueDate.AddDays(-5),
            dueDate,
            "EGP",
            Now);
        inv.AddLine(ChargeType.Rent, "Rent", 1m, amount, Now);
        inv.Issue(number, dueDate.AddDays(-5), Now);
        return inv;
    }

    [Fact]
    public void AutoAllocation_OldestOverdueFirst_AndOverpaymentKeepCredit()
    {
        var orgId = Guid.NewGuid();
        var residentId = Guid.NewGuid();

        var invFeb = CreateIssuedInvoice(orgId, residentId, 5000m, new DateOnly(2026, 2, 5), "INV-1");
        invFeb.MarkOverdue(new DateOnly(2026, 4, 10), Now);
        var invApr = CreateIssuedInvoice(orgId, residentId, 7000m, new DateOnly(2026, 4, 15), "INV-2");

        var payment = Payment.Create(
            orgId,
            residentId,
            15000m,
            "EGP",
            PaymentMethod.BankTransfer,
            PaymentProviderType.Manual,
            Now,
            overpaymentPolicy: OverpaymentPolicy.KeepUnappliedCredit);
        payment.MarkSucceeded(Now);

        var plan = PaymentAutoAllocationEngine.BuildPlan(
            payment,
            [invApr, invFeb],
            AllocationStrategy.OldestOverdueFirst,
            null,
            new DateOnly(2026, 4, 10));

        Assert.Equal(2, plan.PlannedAllocations.Count);
        Assert.Equal(invFeb.Id, plan.PlannedAllocations[0].InvoiceId);
        Assert.Equal(5000m, plan.PlannedAllocations[0].Amount);
        Assert.Equal(invApr.Id, plan.PlannedAllocations[1].InvoiceId);
        Assert.Equal(7000m, plan.PlannedAllocations[1].Amount);
        Assert.Equal(12000m, plan.TotalAllocated);
        Assert.Equal(3000m, plan.RemainingUnapplied);

    }
    
    [Fact]
    public void RejectOverpaymentPolicy_ShouldThrowWhenBalanceRemainsUnapplied()
    {
        var orgId = Guid.NewGuid();
        var residentId = Guid.NewGuid();
        var inv = CreateIssuedInvoice(orgId, residentId, 5000m, new DateOnly(2026, 4, 15), "INV-1");

        var payment = Payment.Create(
            orgId,
            residentId,
            6000m,
            "EGP",
            PaymentMethod.Card,
            PaymentProviderType.Stripe,
            Now,
            overpaymentPolicy: OverpaymentPolicy.RejectOverpayment);
        payment.MarkSucceeded(Now);

        Assert.Throws<InvalidOperationException>(() => PaymentAutoAllocationEngine.BuildPlan(
            payment,
            [inv],
            AllocationStrategy.OldestDueFirst,
            null,
            new DateOnly(2026, 4, 10)));
    }

    [Fact]
    public void ReconciliationEngine_ShouldDetectMatchedAndAllMismatchTypesWithoutMutatingPayments()
    {
        var orgId = Guid.NewGuid();
        var batchId = Guid.NewGuid();

        var pMatched = Payment.Create(orgId, Guid.NewGuid(), 10000m, "EGP", PaymentMethod.Card, PaymentProviderType.Stripe, Now);
        pMatched.MarkSucceeded(Now, providerTransactionId: "tx_match", providerFee: 250m);
        pMatched.RecordSettlement(250m, Now, Now);

        var pAmountDiff = Payment.Create(orgId, Guid.NewGuid(), 6500m, "EGP", PaymentMethod.Card, PaymentProviderType.Stripe, Now);
        pAmountDiff.MarkSucceeded(Now, providerTransactionId: "tx_amt_diff");

        var pFeeDiff = Payment.Create(orgId, Guid.NewGuid(), 4000m, "EGP", PaymentMethod.Card, PaymentProviderType.Stripe, Now);
        pFeeDiff.MarkSucceeded(Now, providerTransactionId: "tx_fee_diff");
        pFeeDiff.RecordSettlement(100m, Now, Now);

        var pMissingAtProvider = Payment.Create(orgId, Guid.NewGuid(), 3000m, "EGP", PaymentMethod.Card, PaymentProviderType.Stripe, Now);
        pMissingAtProvider.MarkSucceeded(Now, providerTransactionId: "tx_only_internal");

        var providerSnapshots = new[]
        {
            new ProviderTransactionSnapshot("tx_match", 10000m, 250m, 9750m, "EGP"),
            new ProviderTransactionSnapshot("tx_amt_diff", 7500m, 150m, 7350m, "EGP"),
            new ProviderTransactionSnapshot("tx_fee_diff", 4000m, 180m, 3820m, "EGP"),
            new ProviderTransactionSnapshot("tx_unknown", 2200m, 50m, 2150m, "EGP"),
            new ProviderTransactionSnapshot("tx_match", 10000m, 250m, 9750m, "EGP") // duplicate in batch
        };

        var records = PaymentReconciliationEngine.Reconcile(
            orgId,
            batchId,
            PaymentProviderType.Stripe,
            [pMatched, pAmountDiff, pFeeDiff, pMissingAtProvider],
            providerSnapshots,
            Now);

        Assert.Contains(records, r => r.ProviderTransactionId == "tx_match" && r.Status == ReconciliationStatus.Matched);
        Assert.Contains(records, r => r.ProviderTransactionId == "tx_amt_diff" && r.Status == ReconciliationStatus.AmountMismatch);
        Assert.Contains(records, r => r.ProviderTransactionId == "tx_fee_diff" && r.Status == ReconciliationStatus.FeeMismatch);
        Assert.Contains(records, r => r.ProviderTransactionId == "tx_unknown" && r.Status == ReconciliationStatus.MissingInternalPayment);
        Assert.Contains(records, r => r.ProviderTransactionId == "tx_only_internal" && r.Status == ReconciliationStatus.MissingProviderTransaction);
        Assert.Contains(records, r => r.ProviderTransactionId == "tx_match" && r.Status == ReconciliationStatus.DuplicateProviderTransaction);

        // Payments remain untouched
        Assert.Equal(6500m, pAmountDiff.Amount);
        Assert.Equal(100m, pFeeDiff.ProviderFee);

        // Resolution works cleanly on exception record
        var mismatch = records.First(r => r.Status == ReconciliationStatus.AmountMismatch);
        mismatch.Resolve(Guid.NewGuid(), "Verified partial chargeback adjustment", Now.AddHours(1));
        Assert.Equal(ReconciliationStatus.Resolved, mismatch.Status);
    }
}
