using PropertyManagement.Application.Abstractions.Payments;
using PropertyManagement.Domain.Entities.Payments.Services;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Api.Contracts.Payments;

public sealed record CreatePaymentIntentRequest(
    Guid ResidentId,
    decimal Amount,
    string Currency,
    PaymentMethod PaymentMethod,
    PaymentProviderType Provider,
    Guid? LeaseId = null,
    Guid? TargetInvoiceId = null,
    string? Description = null,
    string? IdempotencyKey = null,
    int? ExpiresInMinutes = null);

public sealed record CreatePaymentRequest(
    Guid ResidentId,
    decimal Amount,
    string Currency,
    PaymentMethod PaymentMethod,
    PaymentProviderType Provider,
    Guid? LeaseId = null,
    Guid? PaymentIntentId = null,
    AllocationStrategy AllocationStrategy = AllocationStrategy.Explicit,
    OverpaymentPolicy OverpaymentPolicy = OverpaymentPolicy.KeepUnappliedCredit,
    IReadOnlyList<RequestedInvoiceAllocation>? Allocations = null,
    string? Reference = null,
    string? MetadataJson = null,
    string? IdempotencyKey = null,
    DateTime? ReceivedAtUtc = null);

public sealed record ConfirmPaymentRequest(
    string? ProviderTransactionId = null,
    AllocationStrategy AllocationStrategy = AllocationStrategy.Explicit,
    IReadOnlyList<RequestedInvoiceAllocation>? Allocations = null,
    string? RowVersion = null);

public sealed record CancelPaymentRequest(
    string Reason,
    string? RowVersion = null);

public sealed record SyncPaymentStatusRequest(
    bool AutoAllocate = true);

public sealed record AllocatePaymentRequest(
    AllocationStrategy Strategy = AllocationStrategy.Explicit,
    IReadOnlyList<RequestedInvoiceAllocation>? Allocations = null,
    string? RowVersion = null);

public sealed record ReversePaymentAllocationRequest(
    string Reason,
    string? RowVersion = null);

public sealed record RefundPaymentRequest(
    decimal Amount,
    string Reason,
    string? IdempotencyKey = null,
    string? RowVersion = null);

public sealed record RecordProviderSettlementRequest(
    decimal ProviderFee,
    DateTime SettledAtUtc);

public sealed record ReconcilePaymentsRequest(
    PaymentProviderType Provider,
    DateTime FromUtc,
    DateTime ToUtc,
    IReadOnlyList<ProviderTransactionSnapshot>? ProviderTransactionsOverride = null);

public sealed record ResolveReconciliationRecordRequest(
    string ResolutionNotes);

internal static class PaymentHttpRequestHelper
{
    public static string? ResolveIdempotencyKey(string? bodyKey, HttpRequest request)
    {
        if (!string.IsNullOrWhiteSpace(bodyKey))
        {
            return bodyKey.Trim();
        }

        if (request.Headers.TryGetValue("Idempotency-Key", out var headerValues))
        {
            var raw = headerValues.FirstOrDefault()?.Trim();
            return string.IsNullOrWhiteSpace(raw) ? null : raw;
        }

        return null;
    }

    public static string? ResolveRowVersion(string? bodyRowVersion, HttpRequest request)
    {
        if (!string.IsNullOrWhiteSpace(bodyRowVersion))
        {
            return bodyRowVersion.Trim();
        }

        if (request.Headers.TryGetValue("If-Match", out var headerValues))
        {
            var raw = headerValues.FirstOrDefault()?.Trim();
            if (!string.IsNullOrWhiteSpace(raw))
            {
                if (raw.StartsWith("W/", StringComparison.OrdinalIgnoreCase))
                {
                    raw = raw[2..].Trim();
                }

                raw = raw.Trim('"');
                return string.IsNullOrWhiteSpace(raw) ? null : raw;
            }
        }

        return null;
    }
}
