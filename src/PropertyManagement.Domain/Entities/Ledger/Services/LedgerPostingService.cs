using PropertyManagement.Domain.Common;
using PropertyManagement.Domain.Entities.Billing;
using PropertyManagement.Domain.Entities.Payments;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Domain.Entities.Ledger.Services;

public static class LedgerPostingService
{
    public static JournalEntry CreateInvoiceIssuedEntry(
        Invoice invoice,
        string entryNumber,
        string idempotencyKey,
        DateTime utcNow)
    {
        ArgumentNullException.ThrowIfNull(invoice);
        if (invoice.Status is InvoiceStatus.Draft or InvoiceStatus.Void)
        {
            throw new InvalidOperationException($"Cannot post ledger revenue for invoice in '{invoice.Status}' status.");
        }

        if (invoice.Total <= 0m)
        {
            throw new InvalidOperationException("Cannot post invoice ledger entry for zero-total invoice.");
        }

        var entryDateUtc = invoice.IssueDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var entry = JournalEntry.CreateDraft(
            invoice.OrganizationId,
            entryNumber,
            "InvoiceIssued",
            invoice.Id,
            idempotencyKey,
            invoice.Currency,
            $"Invoice {invoice.InvoiceNumber ?? invoice.Id.ToString()} issued",
            entryDateUtc,
            utcNow);

        entry.AddDebit(
            LedgerAccountCode.AccountsReceivable,
            invoice.Total,
            $"Accounts receivable recognized for invoice {invoice.InvoiceNumber ?? invoice.Id.ToString()}");

        var buckets = ComputeRevenueBuckets(invoice, includeAdjustments: false);
        foreach (var (account, amount) in buckets)
        {
            entry.AddCredit(account, amount, DescribeRevenueLine(account, invoice.InvoiceNumber));
        }

        entry.Post(utcNow);
        return entry;
    }

    public static JournalEntry CreateInvoiceAdjustmentEntry(
        Invoice invoice,
        InvoiceAdjustment adjustment,
        string entryNumber,
        string idempotencyKey,
        DateTime utcNow)
    {
        ArgumentNullException.ThrowIfNull(invoice);
        ArgumentNullException.ThrowIfNull(adjustment);
        if (invoice.Status == InvoiceStatus.Draft)
        {
            throw new InvalidOperationException("Cannot post adjustment ledger entry for a Draft invoice.");
        }

        var revenueAccount = ResolveRevenueAccount(adjustment.ChargeType);
        var entry = JournalEntry.CreateDraft(
            invoice.OrganizationId,
            entryNumber,
            "InvoiceAdjusted",
            adjustment.Id,
            idempotencyKey,
            invoice.Currency,
            $"Adjustment on invoice {invoice.InvoiceNumber ?? invoice.Id.ToString()}: {adjustment.Description}",
            adjustment.CreatedAtUtc,
            utcNow);

        if (adjustment.Type == InvoiceAdjustmentType.Debit)
        {
            entry.AddDebit(LedgerAccountCode.AccountsReceivable, adjustment.Amount, $"Debit adjustment ({adjustment.ChargeType})");
            entry.AddCredit(revenueAccount, adjustment.Amount, $"Recognize {adjustment.ChargeType} adjustment revenue");
        }
        else
        {
            entry.AddDebit(revenueAccount, adjustment.Amount, $"Credit adjustment ({adjustment.ChargeType})");
            entry.AddCredit(LedgerAccountCode.AccountsReceivable, adjustment.Amount, $"Reduce accounts receivable for credit adjustment");
        }

        entry.Post(utcNow);
        return entry;
    }

    public static JournalEntry CreateInvoiceVoidReversalEntry(
        Invoice invoice,
        string entryNumber,
        string idempotencyKey,
        DateTime utcNow)
    {
        ArgumentNullException.ThrowIfNull(invoice);
        if (invoice.Status != InvoiceStatus.Void)
        {
            throw new InvalidOperationException($"Cannot post void reversal for invoice in '{invoice.Status}' status.");
        }

        if (!invoice.IssuedAtUtc.HasValue)
        {
            throw new InvalidOperationException("Cannot post void reversal for an invoice that was never issued.");
        }

        if (invoice.Total <= 0m)
        {
            throw new InvalidOperationException("Cannot post void reversal for zero-total invoice.");
        }

        var entry = JournalEntry.CreateDraft(
            invoice.OrganizationId,
            entryNumber,
            "InvoiceVoided",
            invoice.Id,
            idempotencyKey,
            invoice.Currency,
            $"Void reversal for invoice {invoice.InvoiceNumber ?? invoice.Id.ToString()}: {invoice.VoidReason}",
            invoice.VoidedAtUtc ?? utcNow,
            utcNow);

        var buckets = ComputeRevenueBuckets(invoice, includeAdjustments: true);
        foreach (var (account, amount) in buckets)
        {
            entry.AddDebit(account, amount, $"Reverse {account} on voided invoice {invoice.InvoiceNumber ?? invoice.Id.ToString()}");
        }

        entry.AddCredit(
            LedgerAccountCode.AccountsReceivable,
            invoice.Total,
            $"Reverse accounts receivable on voided invoice {invoice.InvoiceNumber ?? invoice.Id.ToString()}");

        entry.Post(utcNow);
        return entry;
    }

    public static JournalEntry CreateReversalEntry(
        JournalEntry originalEntry,
        string entryNumber,
        string idempotencyKey,
        string reason,
        DateTime utcNow)
    {
        ArgumentNullException.ThrowIfNull(originalEntry);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason, nameof(reason));
        if (originalEntry.Status != LedgerEntryStatus.Posted)
        {
            throw new InvalidOperationException("Only posted journal entries can be reversed.");
        }

        var entry = JournalEntry.CreateDraft(
            originalEntry.OrganizationId,
            entryNumber,
            "JournalEntryReversal",
            originalEntry.Id,
            idempotencyKey,
            originalEntry.Currency,
            $"Reversal of {originalEntry.EntryNumber}: {reason.Trim()}",
            utcNow,
            utcNow);

        foreach (var line in originalEntry.Lines)
        {
            if (line.Debit > 0m)
            {
                entry.AddCredit(line.AccountCode, line.Debit, $"Reversal: {line.Description}");
            }
            else if (line.Credit > 0m)
            {
                entry.AddDebit(line.AccountCode, line.Credit, $"Reversal: {line.Description}");
            }
        }

        entry.Post(utcNow);
        return entry;
    }

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

    private static LedgerAccountCode ResolveRevenueAccount(ChargeType chargeType) => chargeType switch
    {
        ChargeType.Rent => LedgerAccountCode.RentalRevenue,
        ChargeType.LateFee => LedgerAccountCode.LateFeeRevenue,
        _ => LedgerAccountCode.OtherRevenue
    };

    private static string DescribeRevenueLine(LedgerAccountCode account, string? invoiceNumber) => account switch
    {
        LedgerAccountCode.RentalRevenue => $"Rental revenue for invoice {invoiceNumber ?? "N/A"}",
        LedgerAccountCode.LateFeeRevenue => $"Late fee revenue for invoice {invoiceNumber ?? "N/A"}",
        _ => $"Other charge revenue for invoice {invoiceNumber ?? "N/A"}"
    };

    private static IReadOnlyList<(LedgerAccountCode Account, decimal Amount)> ComputeRevenueBuckets(
        Invoice invoice,
        bool includeAdjustments)
    {
        var targetTotal = includeAdjustments ? invoice.Total : invoice.Subtotal;
        var buckets = new Dictionary<LedgerAccountCode, decimal>
        {
            [LedgerAccountCode.RentalRevenue] = 0m,
            [LedgerAccountCode.LateFeeRevenue] = 0m,
            [LedgerAccountCode.OtherRevenue] = 0m
        };

        var discountPool = 0m;
        foreach (var line in invoice.Lines)
        {
            if (line.Amount < 0m || line.Type == ChargeType.Discount)
            {
                discountPool = MoneyRounding.Round(discountPool + Math.Abs(line.Amount));
                continue;
            }

            var account = ResolveRevenueAccount(line.Type);
            buckets[account] = MoneyRounding.Round(buckets[account] + line.Amount);
        }

        if (includeAdjustments)
        {
            foreach (var adj in invoice.Adjustments)
            {
                var account = ResolveRevenueAccount(adj.ChargeType);
                if (adj.SignedAmount >= 0m)
                {
                    buckets[account] = MoneyRounding.Round(buckets[account] + adj.SignedAmount);
                }
                else
                {
                    var abs = Math.Abs(adj.SignedAmount);
                    var directDeduction = Math.Min(buckets[account], abs);
                    buckets[account] = MoneyRounding.Round(buckets[account] - directDeduction);
                    discountPool = MoneyRounding.Round(discountPool + (abs - directDeduction));
                }
            }
        }

        // Apply any discount/credit pool across positive revenue buckets (Rent first, then Other, then LateFee)
        foreach (var acct in new[] { LedgerAccountCode.RentalRevenue, LedgerAccountCode.OtherRevenue, LedgerAccountCode.LateFeeRevenue })
        {
            if (discountPool <= 0m) break;
            var deduct = Math.Min(buckets[acct], discountPool);
            buckets[acct] = MoneyRounding.Round(buckets[acct] - deduct);
            discountPool = MoneyRounding.Round(discountPool - deduct);
        }

        var positive = buckets
            .Where(kv => kv.Value > 0m)
            .Select(kv => (Account: kv.Key, Amount: kv.Value))
            .ToList();

        if (positive.Count == 0)
        {
            return [(LedgerAccountCode.RentalRevenue, targetTotal)];
        }

        var sum = MoneyRounding.Round(positive.Sum(x => x.Amount));
        if (sum != targetTotal)
        {
            var diff = MoneyRounding.Round(targetTotal - sum);
            var largestIdx = 0;
            for (var i = 1; i < positive.Count; i++)
            {
                if (positive[i].Amount > positive[largestIdx].Amount)
                {
                    largestIdx = i;
                }
            }

            var adjusted = MoneyRounding.Round(positive[largestIdx].Amount + diff);
            if (adjusted > 0m)
            {
                positive[largestIdx] = (positive[largestIdx].Account, adjusted);
            }
            else
            {
                return [(LedgerAccountCode.RentalRevenue, targetTotal)];
            }
        }

        return positive;
    }
}
