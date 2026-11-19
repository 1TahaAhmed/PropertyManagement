using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using PropertyManagement.Application.Abstractions.Payments;
using PropertyManagement.Domain.Common;
using PropertyManagement.Domain.Entities.Payments.Services;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Infrastructure.Payments;

public sealed class SimulatedPaymentProvider : IPaymentProvider
{
    private readonly IOptions<PaymentProviderOptions> _options;
    private readonly ConcurrentDictionary<string, ProviderPaymentResult> _paymentsById = new(StringComparer.Ordinal);
    private readonly ConcurrentBag<ProviderTransactionSnapshot> _settlements = [];

    public SimulatedPaymentProvider(IOptions<PaymentProviderOptions> options)
    {
        _options = options;
    }

    public PaymentProviderType ProviderType => PaymentProviderType.Simulated;

    public void SeedPaymentStatus(string providerPaymentId, ProviderPaymentResult status)
    {
        _paymentsById[providerPaymentId] = status;
    }

    public void SeedSettlementSnapshot(ProviderTransactionSnapshot snapshot)
    {
        _settlements.Add(snapshot);
    }

    public static string ComputeWebhookSignature(string secret, long unixTimestamp, string rawBody)
    {
        var payload = $"{unixTimestamp}.{rawBody}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return $"t={unixTimestamp},v1={Convert.ToHexString(hash).ToLowerInvariant()}";
    }

    public Task<ProviderCreateIntentResult> CreateIntentAsync(
        ProviderCreateIntentRequest request,
        CancellationToken cancellationToken = default)
    {
        var intentId = $"sim_pi_{request.IntentId:N}";
        return Task.FromResult(new ProviderCreateIntentResult(
            Succeeded: true,
            ProviderIntentId: intentId,
            ClientSecret: $"sim_secret_{request.IntentId:N}",
            ActionUrl: $"https://simulated.payments.local/pay/{intentId}",
            RequiresAction: request.PaymentMethod == PaymentMethod.Card));
    }

    public Task<ProviderPaymentResult> CreateOrCapturePaymentAsync(
        ProviderPaymentRequest request,
        CancellationToken cancellationToken = default)
    {
        var providerPaymentId = $"sim_pay_{request.PaymentId:N}";
        var providerTxId = $"sim_tx_{request.PaymentId:N}";
        var customerId = $"sim_cus_{request.ResidentId:N}";
        var reference = request.Reference ?? string.Empty;

        if (reference.Contains("simulate:fail", StringComparison.OrdinalIgnoreCase))
        {
            var failed = new ProviderPaymentResult(
                CallSucceeded: false,
                IsPaid: false,
                IsProcessing: false,
                ProviderPaymentId: providerPaymentId,
                ProviderCustomerId: customerId,
                ProviderTransactionId: null,
                ProviderStatus: "failed",
                ProviderFee: null,
                FailureReason: "Simulated provider declined payment.");
            _paymentsById[providerPaymentId] = failed;
            return Task.FromResult(failed);
        }

        if (reference.Contains("simulate:processing", StringComparison.OrdinalIgnoreCase) ||
            reference.Contains("simulate:pending", StringComparison.OrdinalIgnoreCase))
        {
            var processing = new ProviderPaymentResult(
                CallSucceeded: true,
                IsPaid: false,
                IsProcessing: true,
                ProviderPaymentId: providerPaymentId,
                ProviderCustomerId: customerId,
                ProviderTransactionId: null,
                ProviderStatus: "processing",
                ProviderFee: null);
            _paymentsById[providerPaymentId] = processing;
            return Task.FromResult(processing);
        }

        var simOpts = _options.Value.Simulated;
        var calculatedFee = MoneyRounding.Round(request.Amount * simOpts.FeePercent + simOpts.FixedFee);
        if (calculatedFee >= request.Amount)
        {
            calculatedFee = 0m;
        }

        var succeeded = new ProviderPaymentResult(
            CallSucceeded: true,
            IsPaid: true,
            IsProcessing: false,
            ProviderPaymentId: providerPaymentId,
            ProviderCustomerId: customerId,
            ProviderTransactionId: providerTxId,
            ProviderStatus: "succeeded",
            ProviderFee: calculatedFee);

        _paymentsById[providerPaymentId] = succeeded;
        _settlements.Add(new ProviderTransactionSnapshot(
            providerTxId,
            MoneyRounding.Round(request.Amount),
            calculatedFee,
            MoneyRounding.Round(request.Amount - calculatedFee),
            CurrencyCode.Normalize(request.Currency),
            providerPaymentId,
            DateTime.UtcNow));

        return Task.FromResult(succeeded);
    }

    public Task<ProviderPaymentResult> GetPaymentStatusAsync(
        string providerPaymentId,
        CancellationToken cancellationToken = default)
    {
        if (_paymentsById.TryGetValue(providerPaymentId, out var existing))
        {
            return Task.FromResult(existing);
        }

        return Task.FromResult(new ProviderPaymentResult(
            CallSucceeded: true,
            IsPaid: false,
            IsProcessing: true,
            ProviderPaymentId: providerPaymentId,
            ProviderCustomerId: null,
            ProviderTransactionId: null,
            ProviderStatus: "processing",
            ProviderFee: null));
    }

    public Task<ProviderRefundResult> RefundPaymentAsync(
        ProviderRefundRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Reason.Contains("simulate:fail", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(new ProviderRefundResult(
                CallSucceeded: false,
                IsCompleted: false,
                ProviderRefundId: null,
                ProviderStatus: "failed",
                FailureReason: "Simulated provider rejected refund."));
        }

        return Task.FromResult(new ProviderRefundResult(
            CallSucceeded: true,
            IsCompleted: true,
            ProviderRefundId: $"sim_re_{request.RefundId:N}",
            ProviderStatus: "succeeded"));
    }

    public Task<IReadOnlyList<ProviderTransactionSnapshot>> FetchSettlementReportAsync(
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default)
    {
        var items = _settlements
            .Where(s => !s.SettledAtUtc.HasValue || (s.SettledAtUtc.Value >= fromUtc && s.SettledAtUtc.Value <= toUtc))
            .ToList();
        return Task.FromResult<IReadOnlyList<ProviderTransactionSnapshot>>(items);
    }

    public ProviderWebhookVerificationResult VerifyAndParseWebhook(
        string rawBody,
        IReadOnlyDictionary<string, string> headers,
        DateTime utcNow)
    {
        var secret = _options.Value.Simulated.WebhookSecret;
        if (string.IsNullOrWhiteSpace(secret))
        {
            return InvalidSignature(utcNow, "Simulated webhook secret is not configured.");
        }

        if (!TryGetHeader(headers, "X-Simulated-Signature", out var signatureHeader) &&
            !TryGetHeader(headers, "Simulated-Signature", out signatureHeader))
        {
            return InvalidSignature(utcNow, "Missing X-Simulated-Signature header.");
        }

        if (!TryParseTimestampSignatureHeader(signatureHeader, out var timestampUnix, out var signatures))
        {
            return InvalidSignature(utcNow, "Malformed X-Simulated-Signature header.");
        }

        var expectedSig = ComputeHexHmacSha256(secret, $"{timestampUnix}.{rawBody}");
        if (!signatures.Any(sig => FixedTimeHexEquals(sig, expectedSig)))
        {
            return InvalidSignature(utcNow, "Webhook HMAC signature mismatch.");
        }

        var eventTimestampUtc = DateTimeOffset.FromUnixTimeSeconds(timestampUnix).UtcDateTime;
        var tolerance = Math.Max(30, _options.Value.WebhookToleranceSeconds);
        if (Math.Abs((utcNow - eventTimestampUtc).TotalSeconds) > tolerance)
        {
            return new ProviderWebhookVerificationResult(
                IsSignatureValid: true,
                IsWithinReplayWindow: false,
                ErrorMessage: "Simulated webhook timestamp is outside the allowed replay tolerance window.",
                EventId: null,
                EventType: null,
                EventTimestampUtc: eventTimestampUtc,
                ProviderPaymentId: null,
                ProviderIntentId: null,
                ProviderTransactionId: null,
                ProviderRefundId: null,
                Amount: null,
                FeeAmount: null,
                Currency: null,
                ProviderStatus: null,
                FailureReason: null);
        }

        try
        {
            using var doc = JsonDocument.Parse(rawBody);
            var root = doc.RootElement;
            return new ProviderWebhookVerificationResult(
                IsSignatureValid: true,
                IsWithinReplayWindow: true,
                ErrorMessage: null,
                EventId: GetString(root, "eventId") ?? GetString(root, "id"),
                EventType: GetString(root, "eventType") ?? GetString(root, "type"),
                EventTimestampUtc: eventTimestampUtc,
                ProviderPaymentId: GetString(root, "providerPaymentId"),
                ProviderIntentId: GetString(root, "providerIntentId"),
                ProviderTransactionId: GetString(root, "providerTransactionId"),
                ProviderRefundId: GetString(root, "providerRefundId"),
                Amount: GetDecimal(root, "amount"),
                FeeAmount: GetDecimal(root, "feeAmount"),
                Currency: GetString(root, "currency"),
                ProviderStatus: GetString(root, "providerStatus"),
                FailureReason: GetString(root, "failureReason"));
        }
        catch (JsonException ex)
        {
            return new ProviderWebhookVerificationResult(
                IsSignatureValid: true,
                IsWithinReplayWindow: true,
                ErrorMessage: $"Invalid simulated webhook JSON payload: {ex.Message}",
                EventId: null,
                EventType: null,
                EventTimestampUtc: eventTimestampUtc,
                ProviderPaymentId: null,
                ProviderIntentId: null,
                ProviderTransactionId: null,
                ProviderRefundId: null,
                Amount: null,
                FeeAmount: null,
                Currency: null,
                ProviderStatus: null,
                FailureReason: null);
        }
    }

    private static ProviderWebhookVerificationResult InvalidSignature(DateTime utcNow, string message) =>
        new(false, false, message, null, null, utcNow, null, null, null, null, null, null, null, null, null);

    private static bool TryGetHeader(IReadOnlyDictionary<string, string> headers, string name, out string value)
    {
        foreach (var kvp in headers)
        {
            if (string.Equals(kvp.Key, name, StringComparison.OrdinalIgnoreCase))
            {
                value = kvp.Value;
                return true;
            }
        }
        value = string.Empty;
        return false;
    }

    private static bool TryParseTimestampSignatureHeader(string header, out long timestamp, out List<string> v1Signatures)
    {
        timestamp = 0;
        v1Signatures = [];
        var hasTs = false;
        foreach (var part in header.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var idx = part.IndexOf('=');
            if (idx <= 0) continue;
            var key = part[..idx].Trim();
            var val = part[(idx + 1)..].Trim();
            if (key == "t" && long.TryParse(val, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                timestamp = parsed;
                hasTs = true;
            }
            else if (key == "v1" && !string.IsNullOrWhiteSpace(val))
            {
                v1Signatures.Add(val);
            }
        }
        return hasTs && v1Signatures.Count > 0;
    }

    private static string ComputeHexHmacSha256(string secret, string payload)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
    }

    private static bool FixedTimeHexEquals(string a, string b)
    {
        var bytesA = Encoding.UTF8.GetBytes(a.ToLowerInvariant());
        var bytesB = Encoding.UTF8.GetBytes(b.ToLowerInvariant());
        return bytesA.Length == bytesB.Length && CryptographicOperations.FixedTimeEquals(bytesA, bytesB);
    }

    private static string? GetString(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var val) && val.ValueKind == JsonValueKind.String ? val.GetString() : null;

    private static decimal? GetDecimal(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var val) && val.ValueKind == JsonValueKind.Number && val.TryGetDecimal(out var dec) ? dec : null;
}
