using System.Text.Json;
using PropertyManagement.Application.Abstractions.Audit;
using PropertyManagement.Application.Abstractions.Ledger;
using PropertyManagement.Domain.Entities.Audit;
using PropertyManagement.Domain.Entities.Billing;
using PropertyManagement.Domain.Entities.Ledger.Services;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Application.Features.Billing.Common;

internal static class BillingLedgerHelper
{
    public static async Task PostInvoiceIssuedIfNeededAsync(
        Invoice invoice,
        ILedgerRepository ledgerRepository,
        IJournalEntryNumberGenerator journalEntryNumberGenerator,
        IFinancialAuditLogRepository auditRepository,
        Guid? actorUserId,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        if (invoice.Status is InvoiceStatus.Draft or InvoiceStatus.Void)
        {
            return;
        }

        if (invoice.Total > 0m)
        {
            var key = $"invoice-issued:{invoice.Id}";
            if (!await ledgerRepository.ExistsByIdempotencyKeyAsync(
                    invoice.OrganizationId,
                    "InvoiceIssued",
                    invoice.Id,
                    key,
                    cancellationToken))
            {
                var entryNumber = await journalEntryNumberGenerator.GenerateNextAsync(
                    invoice.OrganizationId,
                    utcNow,
                    cancellationToken);
                var entry = LedgerPostingService.CreateInvoiceIssuedEntry(
                    invoice,
                    entryNumber,
                    key,
                    utcNow);
                await ledgerRepository.AddAsync(entry, cancellationToken);
            }
        }

        await RecordAuditAsync(
            auditRepository,
            invoice.OrganizationId,
            "Invoice.Issued",
            "Invoice",
            invoice.Id,
            new
            {
                invoice.InvoiceNumber,
                invoice.Total,
                invoice.Currency,
                invoice.IssueDate,
                invoice.DueDate
            },
            utcNow,
            actorUserId,
            cancellationToken);
    }

    public static async Task PostInvoiceAdjustedIfNeededAsync(
        Invoice invoice,
        InvoiceAdjustment adjustment,
        ILedgerRepository ledgerRepository,
        IJournalEntryNumberGenerator journalEntryNumberGenerator,
        IFinancialAuditLogRepository auditRepository,
        Guid? actorUserId,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var key = $"invoice-adj:{adjustment.Id}";
        if (!await ledgerRepository.ExistsByIdempotencyKeyAsync(
                invoice.OrganizationId,
                "InvoiceAdjusted",
                adjustment.Id,
                key,
                cancellationToken))
        {
            var entryNumber = await journalEntryNumberGenerator.GenerateNextAsync(
                invoice.OrganizationId,
                utcNow,
                cancellationToken);
            var entry = LedgerPostingService.CreateInvoiceAdjustmentEntry(
                invoice,
                adjustment,
                entryNumber,
                key,
                utcNow);
            await ledgerRepository.AddAsync(entry, cancellationToken);
        }

        await RecordAuditAsync(
            auditRepository,
            invoice.OrganizationId,
            "Invoice.Adjusted",
            "Invoice",
            invoice.Id,
            new
            {
                AdjustmentId = adjustment.Id,
                Type = adjustment.Type.ToString(),
                ChargeType = adjustment.ChargeType.ToString(),
                adjustment.Amount,
                invoice.Total,
                invoice.BalanceDue
            },
            utcNow,
            actorUserId,
            cancellationToken);
    }

    public static async Task PostInvoiceVoidedIfNeededAsync(
        Invoice invoice,
        bool wasIssuedBeforeVoid,
        ILedgerRepository ledgerRepository,
        IJournalEntryNumberGenerator journalEntryNumberGenerator,
        IFinancialAuditLogRepository auditRepository,
        Guid? actorUserId,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        if (wasIssuedBeforeVoid && invoice.Total > 0m)
        {
            var key = $"invoice-void:{invoice.Id}";
            if (!await ledgerRepository.ExistsByIdempotencyKeyAsync(
                    invoice.OrganizationId,
                    "InvoiceVoided",
                    invoice.Id,
                    key,
                    cancellationToken))
            {
                var entryNumber = await journalEntryNumberGenerator.GenerateNextAsync(
                    invoice.OrganizationId,
                    utcNow,
                    cancellationToken);
                var entry = LedgerPostingService.CreateInvoiceVoidReversalEntry(
                    invoice,
                    entryNumber,
                    key,
                    utcNow);
                await ledgerRepository.AddAsync(entry, cancellationToken);
            }
        }

        await RecordAuditAsync(
            auditRepository,
            invoice.OrganizationId,
            "Invoice.Voided",
            "Invoice",
            invoice.Id,
            new
            {
                invoice.InvoiceNumber,
                WasIssued = wasIssuedBeforeVoid,
                invoice.Total,
                invoice.VoidReason
            },
            utcNow,
            actorUserId,
            cancellationToken);
    }

    private static async Task RecordAuditAsync(
        IFinancialAuditLogRepository auditRepository,
        Guid organizationId,
        string action,
        string entityType,
        Guid entityId,
        object details,
        DateTime utcNow,
        Guid? actorUserId,
        CancellationToken cancellationToken)
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
                actorUserId),
            cancellationToken);
    }
}
