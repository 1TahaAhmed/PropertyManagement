using PropertyManagement.Domain.Common;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Domain.Entities.Payments.Services;

public sealed record ProviderTransactionSnapshot(
    string ProviderTransactionId,
    decimal GrossAmount,
    decimal FeeAmount,
    decimal NetAmount,
    string Currency,
    string? ProviderPaymentId = null,
    DateTime? SettledAtUtc = null);

public static class PaymentReconciliationEngine
{
    public static IReadOnlyList<PaymentReconciliationRecord> Reconcile(
        Guid organizationId,
        Guid batchId,
        PaymentProviderType provider,
        IReadOnlyCollection<Payment> internalPayments,
        IReadOnlyCollection<ProviderTransactionSnapshot> providerTransactions,
        DateTime utcNow)
    {
        if (organizationId == Guid.Empty) throw new ArgumentException("Organization ID cannot be empty.", nameof(organizationId));
        if (batchId == Guid.Empty) throw new ArgumentException("Batch ID cannot be empty.", nameof(batchId));

        var results = new List<PaymentReconciliationRecord>();
        var internalByTxId = new Dictionary<string, Payment>(StringComparer.Ordinal);
        var internalByProviderPayId = new Dictionary<string, Payment>(StringComparer.Ordinal);
        var matchedInternalIds = new HashSet<Guid>();

        foreach (var p in internalPayments.Where(p => p.OrganizationId == organizationId && p.Provider == provider))
        {
            if (!string.IsNullOrWhiteSpace(p.ProviderTransactionId))
            {
                internalByTxId.TryAdd(p.ProviderTransactionId, p);
            }

            if (!string.IsNullOrWhiteSpace(p.ProviderPaymentId))
            {
                internalByProviderPayId.TryAdd(p.ProviderPaymentId, p);
            }
        }

        var seenProviderTxIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var tx in providerTransactions)
        {
            if (string.IsNullOrWhiteSpace(tx.ProviderTransactionId))
            {
                continue;
            }

            var txId = tx.ProviderTransactionId.Trim();
            if (!seenProviderTxIds.Add(txId))
            {
                results.Add(PaymentReconciliationRecord.Create(
                    organizationId,
                    batchId,
                    provider,
                    tx.Currency,
                    ReconciliationStatus.DuplicateProviderTransaction,
                    utcNow,
                    providerTransactionId: txId,
                    providerAmount: tx.GrossAmount,
                    providerFee: tx.FeeAmount,
                    providerNetAmount: tx.NetAmount,
                    mismatchDetails: $"Duplicate provider transaction '{txId}' appeared in settlement batch."));
                continue;
            }

            Payment? internalPayment = null;
            if (!internalByTxId.TryGetValue(txId, out internalPayment) &&
                !string.IsNullOrWhiteSpace(tx.ProviderPaymentId))
            {
                internalByProviderPayId.TryGetValue(tx.ProviderPaymentId.Trim(), out internalPayment);
            }

            if (internalPayment is null)
            {
                results.Add(PaymentReconciliationRecord.Create(
                    organizationId,
                    batchId,
                    provider,
                    tx.Currency,
                    ReconciliationStatus.MissingInternalPayment,
                    utcNow,
                    providerTransactionId: txId,
                    providerAmount: tx.GrossAmount,
                    providerFee: tx.FeeAmount,
                    providerNetAmount: tx.NetAmount,
                    mismatchDetails: $"Provider transaction '{txId}' has no matching internal payment."));
                continue;
            }

            matchedInternalIds.Add(internalPayment.Id);

            var grossDiff = MoneyRounding.Round(tx.GrossAmount) != internalPayment.Amount;
            var currencyDiff = !string.Equals(
                CurrencyCode.Normalize(tx.Currency),
                internalPayment.Currency,
                StringComparison.Ordinal);
            var feeDiff = internalPayment.SettledAtUtc.HasValue &&
                          MoneyRounding.Round(tx.FeeAmount) != internalPayment.ProviderFee;
            var expectedNet = MoneyRounding.Round(internalPayment.Amount - MoneyRounding.Round(tx.FeeAmount));
            var netDiff = MoneyRounding.Round(tx.NetAmount) != expectedNet ||
                          (internalPayment.SettledAtUtc.HasValue && MoneyRounding.Round(tx.NetAmount) != internalPayment.NetSettlementAmount);

            ReconciliationStatus status;
            string? details = null;

            if (grossDiff || currencyDiff)
            {
                status = ReconciliationStatus.AmountMismatch;
                details = $"Internal {internalPayment.Amount:0.00} {internalPayment.Currency} vs provider {tx.GrossAmount:0.00} {tx.Currency}.";
            }
            else if (feeDiff)
            {
                status = ReconciliationStatus.FeeMismatch;
                details = $"Internal fee {internalPayment.ProviderFee:0.00} vs provider fee {tx.FeeAmount:0.00}.";
            }
            else if (netDiff)
            {
                status = ReconciliationStatus.SettlementMismatch;
                details = $"Expected net {expectedNet:0.00} vs provider net {tx.NetAmount:0.00}.";
            }
            else
            {
                status = ReconciliationStatus.Matched;
            }

            results.Add(PaymentReconciliationRecord.Create(
                organizationId,
                batchId,
                provider,
                internalPayment.Currency,
                status,
                utcNow,
                internalPaymentId: internalPayment.Id,
                providerTransactionId: txId,
                expectedAmount: internalPayment.Amount,
                providerAmount: tx.GrossAmount,
                expectedFee: internalPayment.SettledAtUtc.HasValue ? internalPayment.ProviderFee : tx.FeeAmount,
                providerFee: tx.FeeAmount,
                expectedNetAmount: internalPayment.SettledAtUtc.HasValue ? internalPayment.NetSettlementAmount : expectedNet,
                providerNetAmount: tx.NetAmount,
                mismatchDetails: details));
        }

        foreach (var internalPayment in internalPayments.Where(p =>
            p.OrganizationId == organizationId &&
            p.Provider == provider &&
            p.Status is PaymentStatus.Succeeded or PaymentStatus.PartiallyRefunded or PaymentStatus.Refunded &&
            !matchedInternalIds.Contains(p.Id)))
        {
            results.Add(PaymentReconciliationRecord.Create(
                organizationId,
                batchId,
                provider,
                internalPayment.Currency,
                ReconciliationStatus.MissingProviderTransaction,
                utcNow,
                internalPaymentId: internalPayment.Id,
                providerTransactionId: internalPayment.ProviderTransactionId,
                expectedAmount: internalPayment.Amount,
                expectedFee: internalPayment.ProviderFee,
                expectedNetAmount: internalPayment.NetSettlementAmount,
                mismatchDetails: $"Internal succeeded payment '{internalPayment.Id}' was not found in provider settlement batch."));
        }

        return results;
    }
}
