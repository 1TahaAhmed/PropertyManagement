using System.Text.Json;
using PropertyManagement.Application.Abstractions.Audit;
using PropertyManagement.Application.Abstractions.Billing;
using PropertyManagement.Application.Abstractions.Ledger;
using PropertyManagement.Domain.Entities.Audit;
using PropertyManagement.Domain.Entities.Billing;
using PropertyManagement.Domain.Entities.Ledger.Services;
using PropertyManagement.Domain.Entities.Payments;
using PropertyManagement.Domain.Entities.Payments.Services;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Application.Features.Payments.Common;

internal static class PaymentWorkflowHelper
{
    public static async Task<IReadOnlyList<PaymentAllocation>> ApplyAllocationsUnderLockAsync(
        Payment payment,
        AllocationStrategy strategy,
        IReadOnlyList<RequestedInvoiceAllocation>? explicitAllocations,
        IInvoiceRepository invoiceRepository,
        DateTime utcNow,
        Guid? actorUserId,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<Invoice> candidates;
        if (strategy == AllocationStrategy.Explicit &&
            explicitAllocations is { Count: > 0 } &&
            payment.OverpaymentPolicy != OverpaymentPolicy.AutoApplyToOldestInvoices)
        {
            var ids = explicitAllocations.Select(x => x.InvoiceId).Distinct().OrderBy(id => id).ToList();
            foreach (var invId in ids)
            {
                await invoiceRepository.AcquireInvoiceLockAsync(invId, payment.OrganizationId, cancellationToken);
            }

            candidates = await invoiceRepository.GetByIdsAsync(ids, payment.OrganizationId, cancellationToken);
            foreach (var inv in candidates)
            {
                invoiceRepository.Detach(inv);
            }
            candidates = await invoiceRepository.GetByIdsAsync(ids, payment.OrganizationId, cancellationToken);
        }
        else
        {
            var initialOutstanding = await invoiceRepository.GetOutstandingByResidentAsync(
                payment.ResidentId, payment.OrganizationId, payment.Currency, cancellationToken);

            var explicitIds = explicitAllocations?.Select(x => x.InvoiceId) ?? [];
            var allIds = initialOutstanding.Select(i => i.Id).Concat(explicitIds).Distinct().OrderBy(id => id).ToList();

            foreach (var invId in allIds)
            {
                await invoiceRepository.AcquireInvoiceLockAsync(invId, payment.OrganizationId, cancellationToken);
            }

            foreach (var inv in initialOutstanding)
            {
                invoiceRepository.Detach(inv);
            }

            var outstanding = await invoiceRepository.GetOutstandingByResidentAsync(
                payment.ResidentId, payment.OrganizationId, payment.Currency, cancellationToken);

            if (explicitAllocations is { Count: > 0 })
            {
                var missingIds = explicitAllocations.Select(e => e.InvoiceId)
                    .Except(outstanding.Select(o => o.Id))
                    .Distinct()
                    .ToList();
                if (missingIds.Count > 0)
                {
                    var extra = await invoiceRepository.GetByIdsAsync(missingIds, payment.OrganizationId, cancellationToken);
                    candidates = outstanding.Concat(extra).ToList();
                }
                else
                {
                    candidates = outstanding;
                }
            }
            else
            {
                candidates = outstanding;
            }
        }

        var asOfDate = DateOnly.FromDateTime(utcNow);
        var plan = PaymentAutoAllocationEngine.BuildPlan(
            payment,
            candidates,
            strategy,
            explicitAllocations,
            asOfDate);

        if (plan.PlannedAllocations.Count == 0)
        {
            payment.RecordUnappliedCreditNotice();
            return [];
        }

        var invoiceById = candidates.ToDictionary(i => i.Id);
        var createdAllocations = new List<PaymentAllocation>(plan.PlannedAllocations.Count);

        foreach (var item in plan.PlannedAllocations)
        {
            var invoice = invoiceById[item.InvoiceId];
            var allocation = payment.AllocateToInvoice(invoice.Id, item.Amount, invoice.Currency, utcNow, actorUserId);
            invoice.RecordPaymentAllocation(item.Amount, utcNow);
            invoiceRepository.Update(invoice);
            createdAllocations.Add(allocation);
        }

        payment.RecordUnappliedCreditNotice();
        return createdAllocations;
    }

    public static async Task PostReceiptLedgerIfMissingAsync(
        Payment payment,
        decimal allocatedAtReceipt,
        ILedgerRepository ledgerRepository,
        IJournalEntryNumberGenerator journalEntryNumberGenerator,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var key = $"receipt:{payment.Id}";
        if (await ledgerRepository.ExistsByIdempotencyKeyAsync(
                payment.OrganizationId,
                "PaymentSucceeded",
                payment.Id,
                key,
                cancellationToken))
        {
            return;
        }

        var entryNumber = await journalEntryNumberGenerator.GenerateNextAsync(payment.OrganizationId, utcNow, cancellationToken);
        var entry = LedgerPostingService.CreatePaymentReceiptEntry(
            payment,
            entryNumber,
            key,
            allocatedAtReceipt,
            utcNow);

        await ledgerRepository.AddAsync(entry, cancellationToken);
    }

    public static async Task RecordAuditAsync(
        IFinancialAuditLogRepository auditRepository,
        Guid organizationId,
        string action,
        string entityType,
        Guid entityId,
        object details,
        DateTime utcNow,
        Guid? actorUserId = null,
        string? correlationId = null,
        CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(details);
        if (json.Length > 3900)
        {
            json = json[..3900];
        }

        await auditRepository.AddAsync(
            FinancialAuditLog.Record(
                organizationId,
                action,
                entityType,
                entityId,
                json,
                utcNow,
                actorUserId,
                correlationId),
            cancellationToken);
    }
}
