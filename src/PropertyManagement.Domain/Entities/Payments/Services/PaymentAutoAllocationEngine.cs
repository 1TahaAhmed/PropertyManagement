using PropertyManagement.Domain.Common;
using PropertyManagement.Domain.Entities.Billing;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Domain.Entities.Payments.Services;

public sealed record RequestedInvoiceAllocation(Guid InvoiceId, decimal Amount);

public sealed record PlannedInvoiceAllocation(Guid InvoiceId, decimal Amount);

public sealed record AllocationPlanResult(
    IReadOnlyList<PlannedInvoiceAllocation> PlannedAllocations,
    decimal TotalAllocated,
    decimal RemainingUnapplied);

public static class PaymentAutoAllocationEngine
{
    public static AllocationPlanResult BuildPlan(
        Payment payment,
        IReadOnlyCollection<Invoice> candidateInvoices,
        AllocationStrategy strategy,
        IReadOnlyList<RequestedInvoiceAllocation>? explicitAllocations,
        DateOnly asOfDate)
    {
        ArgumentNullException.ThrowIfNull(payment);
        ArgumentNullException.ThrowIfNull(candidateInvoices);

        if (payment.Status is not (PaymentStatus.Succeeded or PaymentStatus.PartiallyRefunded))
        {
            throw new InvalidOperationException($"Cannot plan allocations for payment in '{payment.Status}' status.");
        }

        var available = MoneyRounding.Round(payment.UnappliedAmount - payment.PendingRefundAmount);
        if (available <= 0m)
        {
            return new AllocationPlanResult([], 0m, 0m);
        }

        var invoiceById = candidateInvoices.ToDictionary(i => i.Id);
        var planned = new List<PlannedInvoiceAllocation>();
        var remaining = available;

        if (strategy == AllocationStrategy.Explicit)
        {
            if (explicitAllocations is null || explicitAllocations.Count == 0)
            {
                if (payment.OverpaymentPolicy == OverpaymentPolicy.RejectOverpayment && remaining > 0m)
                {
                    throw new InvalidOperationException("OverpaymentPolicy.RejectOverpayment does not permit unapplied payment balance without invoice allocations.");
                }

                return new AllocationPlanResult([], 0m, remaining);
            }

            if (explicitAllocations.Select(x => x.InvoiceId).Distinct().Count() != explicitAllocations.Count)
            {
                throw new ArgumentException("Duplicate invoice IDs are not allowed in explicit allocations.", nameof(explicitAllocations));
            }

            foreach (var item in explicitAllocations)
            {
                if (!invoiceById.TryGetValue(item.InvoiceId, out var invoice))
                {
                    throw new InvalidOperationException($"Target invoice '{item.InvoiceId}' was not found.");
                }

                ValidateInvoiceCompatibility(payment, invoice);

                var amount = MoneyRounding.Round(item.Amount);
                if (amount <= 0m)
                {
                    throw new ArgumentException("Allocation amount must be greater than zero.", nameof(explicitAllocations));
                }

                if (amount > invoice.BalanceDue)
                {
                    throw new InvalidOperationException(
                        $"Allocation amount ({amount:0.00}) exceeds invoice '{invoice.Id}' BalanceDue ({invoice.BalanceDue:0.00}).");
                }

                if (amount > remaining)
                {
                    throw new InvalidOperationException(
                        $"Total requested allocation exceeds available payment unapplied balance ({available:0.00}).");
                }

                planned.Add(new PlannedInvoiceAllocation(invoice.Id, amount));
                remaining = MoneyRounding.Round(remaining - amount);
            }

            if (remaining > 0m && payment.OverpaymentPolicy == OverpaymentPolicy.AutoApplyToOldestInvoices)
            {
                var allocatedIds = planned.Select(p => p.InvoiceId).ToHashSet();
                var additional = OrderInvoices(
                    candidateInvoices.Where(i => !allocatedIds.Contains(i.Id) && IsAllocatable(payment, i)),
                    AllocationStrategy.OldestOverdueFirst,
                    asOfDate);

                remaining = ApplySequential(additional, remaining, planned);
            }
        }
        else
        {
            var ordered = OrderInvoices(
                candidateInvoices.Where(i => IsAllocatable(payment, i)),
                strategy,
                asOfDate);

            remaining = ApplySequential(ordered, remaining, planned);
        }

        if (remaining > 0m && payment.OverpaymentPolicy == OverpaymentPolicy.RejectOverpayment)
        {
            throw new InvalidOperationException(
                $"Overpayment of {remaining:0.00} {payment.Currency} is rejected by the payment's OverpaymentPolicy.");
        }

        var totalAllocated = MoneyRounding.Round(planned.Sum(p => p.Amount));
        return new AllocationPlanResult(planned, totalAllocated, remaining);
    }

    private static decimal ApplySequential(
        IEnumerable<Invoice> invoices,
        decimal remaining,
        List<PlannedInvoiceAllocation> planned)
    {
        foreach (var invoice in invoices)
        {
            if (remaining <= 0m)
            {
                break;
            }

            var toAllocate = Math.Min(remaining, invoice.BalanceDue);
            toAllocate = MoneyRounding.Round(toAllocate);
            if (toAllocate <= 0m)
            {
                continue;
            }

            planned.Add(new PlannedInvoiceAllocation(invoice.Id, toAllocate));
            remaining = MoneyRounding.Round(remaining - toAllocate);
        }

        return remaining;
    }

    private static IEnumerable<Invoice> OrderInvoices(
        IEnumerable<Invoice> invoices,
        AllocationStrategy strategy,
        DateOnly asOfDate)
    {
        return strategy switch
        {
            AllocationStrategy.OldestOverdueFirst => invoices
                .OrderByDescending(i => i.Status == InvoiceStatus.Overdue || i.DueDate < asOfDate)
                .ThenBy(i => i.DueDate)
                .ThenBy(i => i.IssueDate)
                .ThenBy(i => i.Id),
            AllocationStrategy.OldestDueFirst => invoices
                .OrderBy(i => i.DueDate)
                .ThenBy(i => i.IssueDate)
                .ThenBy(i => i.Id),
            _ => invoices.OrderBy(i => i.DueDate).ThenBy(i => i.Id)
        };
    }

    private static bool IsAllocatable(Payment payment, Invoice invoice) =>
        invoice.OrganizationId == payment.OrganizationId &&
        invoice.ResidentId == payment.ResidentId &&
        string.Equals(invoice.Currency, payment.Currency, StringComparison.Ordinal) &&
        invoice.Status is InvoiceStatus.Issued or InvoiceStatus.PartiallyPaid or InvoiceStatus.Overdue &&
        invoice.BalanceDue > 0m;

    private static void ValidateInvoiceCompatibility(Payment payment, Invoice invoice)
    {
        if (invoice.OrganizationId != payment.OrganizationId)
        {
            throw new InvalidOperationException("Cross-organization payment allocation is forbidden.");
        }

        if (invoice.ResidentId != payment.ResidentId)
        {
            throw new InvalidOperationException($"Invoice '{invoice.Id}' belongs to a different resident than payment '{payment.Id}'.");
        }

        if (!string.Equals(invoice.Currency, payment.Currency, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Currency mismatch between payment ('{payment.Currency}') and invoice ('{invoice.Currency}').");
        }

        if (invoice.Status is not (InvoiceStatus.Issued or InvoiceStatus.PartiallyPaid or InvoiceStatus.Overdue))
        {
            throw new InvalidOperationException($"Invoice '{invoice.Id}' in '{invoice.Status}' status cannot receive payment allocations.");
        }

        if (invoice.BalanceDue <= 0m)
        {
            throw new InvalidOperationException($"Invoice '{invoice.Id}' has no outstanding BalanceDue.");
        }
    }
}
