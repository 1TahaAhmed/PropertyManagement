using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using PropertyManagement.Application.Abstractions.Payments;
using PropertyManagement.Domain.Common;
using PropertyManagement.Domain.Entities.Payments.Services;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Infrastructure.Payments;

public sealed class StripePaymentProvider(
    HttpClient httpClient,
    IOptions<PaymentProviderOptions> options) : IPaymentProvider
{
    public PaymentProviderType ProviderType => PaymentProviderType.Stripe;

    public async Task<ProviderCreateIntentResult> CreateIntentAsync(
        ProviderCreateIntentRequest request,
        CancellationToken cancellationToken = default)
    {
        var stripe = options.Value.Stripe;
        if (string.IsNullOrWhiteSpace(stripe.SecretKey))
        {
            return new ProviderCreateIntentResult(false, null, null, null, false, "Stripe SecretKey is not configured.");
        }

        var form = new Dictionary<string, string>
        {
            ["amount"] = ToMinorUnits(request.Amount).ToString(CultureInfo.InvariantCulture),
            ["currency"] = request.Currency.Trim().ToLowerInvariant(),
            ["payment_method_types[0]"] = MapPaymentMethodType(request.PaymentMethod),
            ["metadata[organization_id]"] = request.OrganizationId.ToString(),
            ["metadata[intent_id]"] = request.IntentId.ToString(),
            ["metadata[resident_id]"] = request.ResidentId.ToString()
        };
        if (!string.IsNullOrWhiteSpace(request.Description))
        {
            form["description"] = request.Description;
        }

        using var httpReq = BuildRequest(HttpMethod.Post, BuildUri(stripe.BaseUrl, "v1/payment_intents"), stripe.SecretKey, request.IdempotencyKey);
        httpReq.Content = new FormUrlEncodedContent(form);

        using var response = await httpClient.SendAsync(httpReq, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return new ProviderCreateIntentResult(false, null, null, null, false, ExtractStripeError(body, response.ReasonPhrase));
        }

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        var status = GetString(root, "status");
        var actionUrl = root.TryGetProperty("next_action", out var na) &&
                        na.ValueKind == JsonValueKind.Object &&
                        na.TryGetProperty("redirect_to_url", out var rtu) &&
                        rtu.ValueKind == JsonValueKind.Object
            ? GetString(rtu, "url")
            : null;

        return new ProviderCreateIntentResult(
            Succeeded: true,
            ProviderIntentId: GetString(root, "id"),
            ClientSecret: GetString(root, "client_secret"),
            ActionUrl: actionUrl,
            RequiresAction: string.Equals(status, "requires_action", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(status, "requires_payment_method", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(status, "requires_confirmation", StringComparison.OrdinalIgnoreCase));
    }

    public async Task<ProviderPaymentResult> CreateOrCapturePaymentAsync(
        ProviderPaymentRequest request,
        CancellationToken cancellationToken = default)
    {
        var stripe = options.Value.Stripe;
        if (string.IsNullOrWhiteSpace(stripe.SecretKey))
        {
            return new ProviderPaymentResult(false, false, false, null, null, null, null, null, "Stripe SecretKey is not configured.");
        }

        HttpRequestMessage httpReq;
        if (!string.IsNullOrWhiteSpace(request.ProviderIntentId))
        {
            var url = BuildUri(stripe.BaseUrl, $"v1/payment_intents/{Uri.EscapeDataString(request.ProviderIntentId)}/confirm");
            httpReq = BuildRequest(HttpMethod.Post, url, stripe.SecretKey, request.IdempotencyKey);
            httpReq.Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["expand[0]"] = "latest_charge.balance_transaction"
            });
        }
        else
        {
            var url = BuildUri(stripe.BaseUrl, "v1/payment_intents");
            httpReq = BuildRequest(HttpMethod.Post, url, stripe.SecretKey, request.IdempotencyKey);
            httpReq.Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["amount"] = ToMinorUnits(request.Amount).ToString(CultureInfo.InvariantCulture),
                ["currency"] = request.Currency.Trim().ToLowerInvariant(),
                ["confirm"] = "true",
                ["payment_method_types[0]"] = MapPaymentMethodType(request.PaymentMethod),
                ["metadata[organization_id]"] = request.OrganizationId.ToString(),
                ["metadata[payment_id]"] = request.PaymentId.ToString(),
                ["metadata[resident_id]"] = request.ResidentId.ToString(),
                ["expand[0]"] = "latest_charge.balance_transaction"
            });
        }

        using (httpReq)
        {
            using var response = await httpClient.SendAsync(httpReq, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return new ProviderPaymentResult(false, false, false, request.ProviderIntentId, null, null, "failed", null, ExtractStripeError(body, response.ReasonPhrase));
            }

            return ParsePaymentIntentResponse(body);
        }
    }

    public async Task<ProviderPaymentResult> GetPaymentStatusAsync(
        string providerPaymentId,
        CancellationToken cancellationToken = default)
    {
        var stripe = options.Value.Stripe;
        if (string.IsNullOrWhiteSpace(stripe.SecretKey))
        {
            return new ProviderPaymentResult(false, false, false, providerPaymentId, null, null, null, null, "Stripe SecretKey is not configured.");
        }

        var url = BuildUri(stripe.BaseUrl, $"v1/payment_intents/{Uri.EscapeDataString(providerPaymentId)}?expand[]=latest_charge.balance_transaction");
        using var httpReq = BuildRequest(HttpMethod.Get, url, stripe.SecretKey);
        using var response = await httpClient.SendAsync(httpReq, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return new ProviderPaymentResult(false, false, true, providerPaymentId, null, null, null, null, ExtractStripeError(body, response.ReasonPhrase));
        }

        return ParsePaymentIntentResponse(body);
    }

    public async Task<ProviderRefundResult> RefundPaymentAsync(
        ProviderRefundRequest request,
        CancellationToken cancellationToken = default)
    {
        var stripe = options.Value.Stripe;
        if (string.IsNullOrWhiteSpace(stripe.SecretKey))
        {
            return new ProviderRefundResult(false, false, null, null, "Stripe SecretKey is not configured.");
        }

        var url = BuildUri(stripe.BaseUrl, "v1/refunds");
        using var httpReq = BuildRequest(HttpMethod.Post, url, stripe.SecretKey, request.IdempotencyKey);
        httpReq.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["payment_intent"] = request.ProviderPaymentId,
            ["amount"] = ToMinorUnits(request.Amount).ToString(CultureInfo.InvariantCulture),
            ["reason"] = "requested_by_customer",
            ["metadata[refund_id]"] = request.RefundId.ToString(),
            ["metadata[organization_id]"] = request.OrganizationId.ToString()
        });

        using var response = await httpClient.SendAsync(httpReq, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return new ProviderRefundResult(false, false, null, "failed", ExtractStripeError(body, response.ReasonPhrase));
        }

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        var status = GetString(root, "status") ?? "pending";
        return new ProviderRefundResult(
            CallSucceeded: true,
            IsCompleted: string.Equals(status, "succeeded", StringComparison.OrdinalIgnoreCase),
            ProviderRefundId: GetString(root, "id"),
            ProviderStatus: status);
    }

    public async Task<IReadOnlyList<ProviderTransactionSnapshot>> FetchSettlementReportAsync(
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default)
    {
        var stripe = options.Value.Stripe;
        if (string.IsNullOrWhiteSpace(stripe.SecretKey))
        {
            return [];
        }

        var gte = new DateTimeOffset(DateTime.SpecifyKind(fromUtc, DateTimeKind.Utc)).ToUnixTimeSeconds();
        var lte = new DateTimeOffset(DateTime.SpecifyKind(toUtc, DateTimeKind.Utc)).ToUnixTimeSeconds();
        var url = BuildUri(stripe.BaseUrl, $"v1/balance_transactions?created[gte]={gte}&created[lte]={lte}&type=charge&limit=100");
        using var httpReq = BuildRequest(HttpMethod.Get, url, stripe.SecretKey);
        using var response = await httpClient.SendAsync(httpReq, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return [];
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var list = new List<ProviderTransactionSnapshot>();
        foreach (var item in data.EnumerateArray())
        {
            var id = GetString(item, "id");
            if (string.IsNullOrWhiteSpace(id)) continue;

            var gross = FromMinorUnits(GetInt64(item, "amount") ?? 0);
            var fee = FromMinorUnits(GetInt64(item, "fee") ?? 0);
            var net = FromMinorUnits(GetInt64(item, "net") ?? 0);
            var currency = CurrencyCode.Normalize(GetString(item, "currency") ?? "USD");
            var source = GetString(item, "source");
            var created = GetInt64(item, "created");
            var settledAt = created.HasValue ? DateTimeOffset.FromUnixTimeSeconds(created.Value).UtcDateTime : (DateTime?)null;

            list.Add(new ProviderTransactionSnapshot(id, gross, fee, net, currency, source, settledAt));
        }

        return list;
    }

    public ProviderWebhookVerificationResult VerifyAndParseWebhook(
        string rawBody,
        IReadOnlyDictionary<string, string> headers,
        DateTime utcNow)
    {
        var webhookSecret = options.Value.Stripe.WebhookSecret;
        if (string.IsNullOrWhiteSpace(webhookSecret))
        {
            return InvalidSignature(utcNow, "Stripe WebhookSecret is not configured.");
        }

        if (!TryGetHeader(headers, "Stripe-Signature", out var signatureHeader))
        {
            return InvalidSignature(utcNow, "Missing Stripe-Signature header.");
        }

        if (!TryParseStripeSignature(signatureHeader, out var timestampUnix, out var v1Signatures))
        {
            return InvalidSignature(utcNow, "Malformed Stripe-Signature header.");
        }

        var expected = ComputeHexHmacSha256(webhookSecret, $"{timestampUnix}.{rawBody}");
        if (!v1Signatures.Any(s => FixedTimeHexEquals(s, expected)))
        {
            return InvalidSignature(utcNow, "Stripe webhook signature verification failed.");
        }

        var eventTime = DateTimeOffset.FromUnixTimeSeconds(timestampUnix).UtcDateTime;
        var tolerance = Math.Max(30, options.Value.WebhookToleranceSeconds);
        if (Math.Abs((utcNow - eventTime).TotalSeconds) > tolerance)
        {
            return new ProviderWebhookVerificationResult(
                true, false, "Stripe webhook timestamp is outside the accepted replay tolerance window.",
                null, null, eventTime, null, null, null, null, null, null, null, null, null);
        }

        try
        {
            using var doc = JsonDocument.Parse(rawBody);
            var root = doc.RootElement;
            var eventId = GetString(root, "id");
            var eventType = GetString(root, "type");
            var createdUnix = GetInt64(root, "created") ?? timestampUnix;
            var createdUtc = DateTimeOffset.FromUnixTimeSeconds(createdUnix).UtcDateTime;

            string? providerPaymentId = null;
            string? providerIntentId = null;
            string? providerTxId = null;
            string? providerRefundId = null;
            decimal? amount = null;
            string? currency = null;
            string? status = null;
            string? failureReason = null;

            if (root.TryGetProperty("data", out var data) && data.TryGetProperty("object", out var obj))
            {
                var objId = GetString(obj, "id");
                var objectKind = GetString(obj, "object");
                status = GetString(obj, "status");
                currency = GetString(obj, "currency")?.ToUpperInvariant();
                var rawAmt = GetInt64(obj, "amount_received") ?? GetInt64(obj, "amount");
                if (rawAmt.HasValue) amount = FromMinorUnits(rawAmt.Value);

                if (string.Equals(objectKind, "refund", StringComparison.OrdinalIgnoreCase) ||
                    (eventType?.StartsWith("refund.", StringComparison.OrdinalIgnoreCase) ?? false))
                {
                    providerRefundId = objId;
                    providerPaymentId = GetString(obj, "payment_intent") ?? GetString(obj, "charge");
                }
                else if (string.Equals(objectKind, "charge", StringComparison.OrdinalIgnoreCase))
                {
                    providerTxId = objId;
                    providerPaymentId = GetString(obj, "payment_intent") ?? objId;
                    providerIntentId = GetString(obj, "payment_intent");
                }
                else
                {
                    providerPaymentId = objId;
                    providerIntentId = objId;
                    providerTxId = GetString(obj, "latest_charge");
                }

                if (obj.TryGetProperty("last_payment_error", out var lpe) && lpe.ValueKind == JsonValueKind.Object)
                {
                    failureReason = GetString(lpe, "message");
                }
                failureReason ??= GetString(obj, "failure_message");
            }

            return new ProviderWebhookVerificationResult(
                true, true, null, eventId, eventType, createdUtc,
                providerPaymentId, providerIntentId, providerTxId, providerRefundId,
                amount, null, currency, status, failureReason);
        }
        catch (JsonException ex)
        {
            return new ProviderWebhookVerificationResult(
                true, true, $"Invalid Stripe webhook payload: {ex.Message}",
                null, null, eventTime, null, null, null, null, null, null, null, null, null);
        }
    }

    private static ProviderPaymentResult ParsePaymentIntentResponse(string body)
    {
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        var id = GetString(root, "id");
        var customerId = GetString(root, "customer");
        var status = GetString(root, "status") ?? "unknown";
        var isPaid = string.Equals(status, "succeeded", StringComparison.OrdinalIgnoreCase);
        var isProcessing = status is "processing" or "requires_capture" or "requires_action" or "requires_confirmation";

        string? txId = null;
        decimal? fee = null;
        if (root.TryGetProperty("latest_charge", out var charge))
        {
            if (charge.ValueKind == JsonValueKind.String)
            {
                txId = charge.GetString();
            }
            else if (charge.ValueKind == JsonValueKind.Object)
            {
                txId = GetString(charge, "id");
                if (charge.TryGetProperty("balance_transaction", out var bt) && bt.ValueKind == JsonValueKind.Object)
                {
                    var feeMinor = GetInt64(bt, "fee");
                    if (feeMinor.HasValue) fee = FromMinorUnits(feeMinor.Value);
                    txId = GetString(bt, "id") ?? txId;
                }
            }
        }

        string? failureReason = null;
        if (root.TryGetProperty("last_payment_error", out var err) && err.ValueKind == JsonValueKind.Object)
        {
            failureReason = GetString(err, "message");
        }

        var callSucceeded = isPaid || isProcessing;
        return new ProviderPaymentResult(callSucceeded, isPaid, isProcessing, id, customerId, txId, status, fee, failureReason);
    }

    private static HttpRequestMessage BuildRequest(HttpMethod method, Uri uri, string secretKey, string? idempotencyKey = null)
    {
        var req = new HttpRequestMessage(method, uri);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", secretKey);
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            req.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey);
        }
        return req;
    }

    private static Uri BuildUri(string baseUrl, string relative) =>
        new($"{baseUrl.TrimEnd('/')}/{relative.TrimStart('/')}");

    private static long ToMinorUnits(decimal amount) =>
        (long)Math.Round(amount * 100m, MidpointRounding.AwayFromZero);

    private static decimal FromMinorUnits(long minorUnits) =>
        MoneyRounding.Round(minorUnits / 100m);

    private static string MapPaymentMethodType(PaymentMethod method) => method switch
    {
        PaymentMethod.Ach => "us_bank_account",
        PaymentMethod.DirectDebit => "sepa_debit",
        _ => "card"
    };

    private static string ExtractStripeError(string body, string? fallback)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("error", out var err) && err.ValueKind == JsonValueKind.Object)
            {
                return GetString(err, "message") ?? fallback ?? "Stripe request failed.";
            }
        }
        catch (JsonException) { }
        return fallback ?? "Stripe request failed.";
    }

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

    private static bool TryParseStripeSignature(string header, out long timestamp, out List<string> v1)
    {
        timestamp = 0;
        v1 = [];
        var hasTs = false;
        foreach (var part in header.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var idx = part.IndexOf('=');
            if (idx <= 0) continue;
            var k = part[..idx].Trim();
            var v = part[(idx + 1)..].Trim();
            if (k == "t" && long.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out var t))
            {
                timestamp = t;
                hasTs = true;
            }
            else if (k == "v1" && !string.IsNullOrWhiteSpace(v))
            {
                v1.Add(v);
            }
        }
        return hasTs && v1.Count > 0;
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

    private static ProviderWebhookVerificationResult InvalidSignature(DateTime utcNow, string message) =>
        new(false, false, message, null, null, utcNow, null, null, null, null, null, null, null, null, null);

    private static string? GetString(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var val) && val.ValueKind == JsonValueKind.String ? val.GetString() : null;

    private static long? GetInt64(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var val) && val.ValueKind == JsonValueKind.Number && val.TryGetInt64(out var n) ? n : null;
}
