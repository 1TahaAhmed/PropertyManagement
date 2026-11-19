using PropertyManagement.Domain.Entities.Audit;
using PropertyManagement.Domain.Entities.Ledger;
using PropertyManagement.Domain.Entities.Ledger.Events;
using PropertyManagement.Domain.Entities.Ledger.Services;
using PropertyManagement.Domain.Entities.Payments;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Domain.Tests.Ledger;

public sealed class LedgerAndAuditTests
{
    private static readonly DateTime Now = new(2026, 4, 15, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void JournalEntry_MustBeBalanced_AndImmutableOncePosted()
    {
        var orgId = Guid.NewGuid();
        var entry = JournalEntry.CreateDraft(
            orgId,
            "JE-2026-000001",
            "ManualTest",
            Guid.NewGuid(),
            "je-key-1",
            "EGP",
            "Test balanced posting",
            Now,
            Now);

        entry.AddDebit(LedgerAccountCode.Cash, 1000m, "Debit cash");
        Assert.Throws<InvalidOperationException>(() => entry.Post(Now)); // only 1 line & unbalanced

        entry.AddCredit(LedgerAccountCode.AccountsReceivable, 900m, "Unbalanced credit");
        Assert.Throws<InvalidOperationException>(() => entry.Post(Now)); // 1000 != 900

        entry.AddCredit(LedgerAccountCode.CustomerUnappliedCredit, 100m, "Balance credit");
        entry.Post(Now);

        Assert.Equal(LedgerEntryStatus.Posted, entry.Status);
        Assert.Equal(1000m, entry.TotalDebits);
        Assert.Equal(1000m, entry.TotalCredits);
        Assert.Contains(entry.DomainEvents, e => e is JournalEntryPostedEvent);

        // Immutable once posted
        Assert.Throws<InvalidOperationException>(() =>
            entry.AddDebit(LedgerAccountCode.Cash, 10m, "Attempt mutation"));
        Assert.Throws<InvalidOperationException>(() =>
            entry.Post(Now.AddMinutes(1)));
    }

    [Fact]
    public void LedgerPostingService_ProviderClearing_SettlementWithFee_AndRefund_ShouldBalance()
    {
        var orgId = Guid.NewGuid();
        var payment = Payment.Create(
            orgId,
            Guid.NewGuid(),
            10000m,
            "EGP",
            PaymentMethod.Card,
            PaymentProviderType.Stripe,
            Now);
        payment.MarkSucceeded(Now, providerTransactionId: "tx_stripe_1");

        // 1. Receipt into Provider Clearing (7,000 allocated to A/R, 3,000 unapplied)
        var receiptEntry = LedgerPostingService.CreatePaymentReceiptEntry(
            payment,
            "JE-2026-000010",
            "receipt-1",
            allocatedAmount: 7000m,
            Now);

        Assert.Equal(10000m, receiptEntry.TotalDebits);
        Assert.Equal(10000m, receiptEntry.TotalCredits);
        Assert.Contains(receiptEntry.Lines, l => l.AccountCode == LedgerAccountCode.PaymentProviderClearing && l.Debit == 10000m);
        Assert.Contains(receiptEntry.Lines, l => l.AccountCode == LedgerAccountCode.AccountsReceivable && l.Credit == 7000m);
        Assert.Contains(receiptEntry.Lines, l => l.AccountCode == LedgerAccountCode.CustomerUnappliedCredit && l.Credit == 3000m);

        // 2. Settlement: 9,700 Cash + 300 Fee Expense = 10,000 Clearing
        payment.RecordSettlement(300m, Now.AddDays(2), Now.AddDays(2));
        var settlementEntry = LedgerPostingService.CreateProviderSettlementEntry(
            payment,
            "JE-2026-000011",
            "settle-1",
            Now.AddDays(2));

        Assert.Equal(10000m, settlementEntry.TotalDebits);
        Assert.Equal(10000m, settlementEntry.TotalCredits);
        Assert.Contains(settlementEntry.Lines, l => l.AccountCode == LedgerAccountCode.Cash && l.Debit == 9700m);
        Assert.Contains(settlementEntry.Lines, l => l.AccountCode == LedgerAccountCode.ProcessingFeeExpense && l.Debit == 300m);
        Assert.Contains(settlementEntry.Lines, l => l.AccountCode == LedgerAccountCode.PaymentProviderClearing && l.Credit == 10000m);

        // 3. Refund unapplied 3,000 -> new reversing journal entry
        var refund = payment.InitiateRefund(3000m, "Return overpayment", Now.AddDays(3), Guid.NewGuid(), "ref-1");
        payment.CompleteRefund(refund.Id, "re_1", Now.AddDays(3));
        var refundEntry = LedgerPostingService.CreateRefundEntry(
            payment,
            refund,
            "JE-2026-000012",
            "refund-1",
            Now.AddDays(3));

        Assert.Equal(3000m, refundEntry.TotalDebits);
        Assert.Equal(3000m, refundEntry.TotalCredits);
        Assert.Contains(refundEntry.Lines, l => l.AccountCode == LedgerAccountCode.CustomerUnappliedCredit && l.Debit == 3000m);
        Assert.Contains(refundEntry.Lines, l => l.AccountCode == LedgerAccountCode.Cash && l.Credit == 3000m);
    }

    [Fact]
    public void FinancialAuditLog_Record_ShouldCaptureImmutableMetadata()
    {
        var orgId = Guid.NewGuid();
        var entityId = Guid.NewGuid();
        var actorId = Guid.NewGuid();

        var log = FinancialAuditLog.Record(
            orgId,
            "Payment.Refunded",
            "Payment",
            entityId,
            "{\"amount\":3000}",
            Now,
            actorId,
            "corr-123");

        Assert.Equal(orgId, log.OrganizationId);
        Assert.Equal("Payment.Refunded", log.Action);
        Assert.Equal(actorId, log.ActorUserId);
        Assert.Equal("corr-123", log.CorrelationId);
    }
}
