using PropertyManagement.Domain.Common;
using PropertyManagement.Domain.Entities.Payments;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Domain.Entities.Ledger.Services;

public static class LedgerPostingService
{
    public static JournalEntry CreatePaymentReceiptEntry(
        Payment payment,
        string entryNumber,
        string idempotencyKey,
        decimal allocatedAmount,
        DateTime utcNow)
    {
        ArgumentNullException.ThrowIfNull(payment);
        if (payment.Status is not (PaymentStatus.Succeeded or PaymentStatus.PartiallyRefunded or PaymentStatus.Refunded))
        {
            throw new InvalidOperationException($"Cannot post ledger receipt for payment in '{payment.Status}' status.");
        }

        var roundedAllocated = MoneyRounding.Round(allocatedAmount);
        if (roundedAllocated < 0m || roundedAllocated > payment.Amount)
        {
            throw new ArgumentException("Allocated amount must be between 0 and payment amount.", nameof(allocatedAmount));
        }

        var unapplied = MoneyRounding.Round(payment.Amount - roundedAllocated);
        var usesClearing = payment.Provider != PaymentProviderType.Manual;
        var debitAccount = usesClearing
            ? LedgerAccountCode.PaymentProviderClearing
            : LedgerAccountCode.Cash;

        var entry = JournalEntry.CreateDraft(
            payment.OrganizationId,
            entryNumber,
            "PaymentSucceeded",
            payment.Id,
            idempotencyKey,
            payment.Currency,
            $"Receipt for payment {payment.PaymentNumber ?? payment.Id.ToString()} via {payment.Provider}",
            payment.ReceivedAtUtc ?? utcNow,
            utcNow);

        entry.AddDebit(
            debitAccount,
            payment.Amount,
            usesClearing ? "Provider clearing receivable" : "Cash/bank funds received");

        if (roundedAllocated > 0m)
        {
            entry.AddCredit(
                LedgerAccountCode.AccountsReceivable,
                roundedAllocated,
                "Settled accounts receivable from invoice allocations");
        }

        if (unapplied > 0m)
        {
            entry.AddCredit(
                LedgerAccountCode.CustomerUnappliedCredit,
                unapplied,
                "Unapplied customer credit liability");
        }

        entry.Post(utcNow);
        return entry;
    }

    public static JournalEntry CreateProviderSettlementEntry(
        Payment payment,
        string entryNumber,
        string idempotencyKey,
        DateTime utcNow)
    {
        ArgumentNullException.ThrowIfNull(payment);
        if (payment.Provider == PaymentProviderType.Manual)
        {
            throw new InvalidOperationException("Manual payments do not settle from a provider clearing account.");
        }

        if (!payment.SettledAtUtc.HasValue)
        {
            throw new InvalidOperationException("Payment settlement details have not been recorded.");
        }

        var entry = JournalEntry.CreateDraft(
            payment.OrganizationId,
            entryNumber,
            "PaymentSettled",
            payment.Id,
            idempotencyKey,
            payment.Currency,
            $"Provider settlement for payment {payment.PaymentNumber ?? payment.Id.ToString()}",
            payment.SettledAtUtc.Value,
            utcNow);

        if (payment.NetSettlementAmount > 0m)
        {
            entry.AddDebit(LedgerAccountCode.Cash, payment.NetSettlementAmount, "Net provider settlement deposited to cash");
        }

        if (payment.ProviderFee > 0m)
        {
            entry.AddDebit(LedgerAccountCode.ProcessingFeeExpense, payment.ProviderFee, "Payment provider processing fee expense");
        }

        entry.AddCredit(LedgerAccountCode.PaymentProviderClearing, payment.Amount, "Clear gross payment from provider clearing account");
        entry.Post(utcNow);
        return entry;
    }

    public static JournalEntry CreateAllocationEntry(
        Payment payment,
        PaymentAllocation allocation,
        string entryNumber,
        string idempotencyKey,
        DateTime utcNow)
    {
        ArgumentNullException.ThrowIfNull(payment);
        ArgumentNullException.ThrowIfNull(allocation);

        var entry = JournalEntry.CreateDraft(
            payment.OrganizationId,
            entryNumber,
            "PaymentAllocation",
            allocation.Id,
            idempotencyKey,
            payment.Currency,
            $"Apply unapplied payment {payment.Id} to invoice {allocation.InvoiceId}",
            allocation.AllocatedAtUtc,
            utcNow);

        entry.AddDebit(LedgerAccountCode.CustomerUnappliedCredit, allocation.Amount, "Consume unapplied customer credit");
        entry.AddCredit(LedgerAccountCode.AccountsReceivable, allocation.Amount, "Reduce invoice accounts receivable");
        entry.Post(utcNow);
        return entry;
    }

    public static JournalEntry CreateAllocationReversalEntry(
        Payment payment,
        PaymentAllocation allocation,
        string entryNumber,
        string idempotencyKey,
        DateTime utcNow)
    {
        ArgumentNullException.ThrowIfNull(payment);
        ArgumentNullException.ThrowIfNull(allocation);

        if (!allocation.IsReversed)
        {
            throw new InvalidOperationException("Allocation must be reversed before posting a reversal journal entry.");
        }

        var entry = JournalEntry.CreateDraft(
            payment.OrganizationId,
            entryNumber,
            "PaymentAllocationReversed",
            allocation.Id,
            idempotencyKey,
            payment.Currency,
            $"Reverse allocation {allocation.Id} from invoice {allocation.InvoiceId}: {allocation.ReversalReason}",
            allocation.ReversedAtUtc ?? utcNow,
            utcNow);

        entry.AddDebit(LedgerAccountCode.AccountsReceivable, allocation.Amount, "Restore accounts receivable on allocation reversal");
        entry.AddCredit(LedgerAccountCode.CustomerUnappliedCredit, allocation.Amount, "Restore unapplied customer credit");
        entry.Post(utcNow);
        return entry;
    }

    public static JournalEntry CreateRefundEntry(
        Payment payment,
        Refund refund,
        string entryNumber,
        string idempotencyKey,
        DateTime utcNow)
    {
        ArgumentNullException.ThrowIfNull(payment);
        ArgumentNullException.ThrowIfNull(refund);

        if (refund.Status != RefundStatus.Succeeded)
        {
            throw new InvalidOperationException("Can only post ledger entry for a succeeded refund.");
        }

        var creditAccount = payment.Provider == PaymentProviderType.Manual || payment.SettledAtUtc.HasValue
            ? LedgerAccountCode.Cash
            : LedgerAccountCode.PaymentProviderClearing;

        var entry = JournalEntry.CreateDraft(
            payment.OrganizationId,
            entryNumber,
            "PaymentRefunded",
            refund.Id,
            idempotencyKey,
            payment.Currency,
            $"Refund {refund.Id} for payment {payment.PaymentNumber ?? payment.Id.ToString()}: {refund.Reason}",
            refund.CompletedAtUtc ?? utcNow,
            utcNow);

        entry.AddDebit(LedgerAccountCode.CustomerUnappliedCredit, refund.Amount, "Refund unapplied customer balance");
        entry.AddCredit(creditAccount, refund.Amount, "Outgoing refund disbursement");
        entry.Post(utcNow);
        return entry;
    }
}
