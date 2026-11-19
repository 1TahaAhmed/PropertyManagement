using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using PropertyManagement.Application.Abstractions.Payments;
using PropertyManagement.Domain.Common;
using PropertyManagement.Domain.Entities.Payments.Services;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Infrastructure.Payments;

public sealed class PaymobPaymentProvider(
    HttpClient httpClient,
    IOptions<PaymentProviderOptions> options) : IPaymentProvider
{
    public PaymentProviderType ProviderType => PaymentProviderType.Paymob;

    public async Task<ProviderCreateIntentResult> CreateIntentAsync(
        ProviderCreateIntentRequest request,
        CancellationToken cancellationToken = default)
    {
        var paymob = options.Value.Paymob;
        if (string.IsNullOrWhiteSpace(paymob.ApiKey) || string.IsNullOrWhiteSpace(paymob.IntegrationId))
        {
            return new ProviderCreateIntentResult(false, null, null, null, false, "Paymob ApiKey or IntegrationId is not configured.");
        }

        var token = await AuthenticateAsync(paymob, cancellationToken);
        if (string.IsNullOrWhiteSpace(token))
        {
            return new ProviderCreateIntentResult(false, null, null, null, false, "Paymob authentication failed.");
        }

        var amountCents = (long)Math.Round(request.Amount * 100m, MidpointRounding.AwayFromZero);
        using var orderResp = await httpClient.PostAsJsonAsync(
            BuildUri(paymob.BaseUrl, "ecommerce/orders"),
            new
            {
                auth_token = token,
                delivery_needed = false,
                amount_cents = amountCents.ToString(CultureInfo.InvariantCulture),
                currency = request.Currency.Trim().ToUpperInvariant(),
                merchant_order_id = request.IntentId.ToString("N"),
                items = Array.Empty<object>()
            },
            cancellationToken);

        if (!orderResp.IsSuccessStatusCode)
        {
            return new ProviderCreateIntentResult(false, null, null, null, false, $"Paymob order creation failed ({(int)orderResp.StatusCode}).");
        }

        var orderJson = await orderResp.Content.ReadAsStringAsync(cancellationToken);
        using var orderDoc = JsonDocument.Parse(orderJson);
        var orderId = orderDoc.RootElement.TryGetProperty("id", out var idEl) ? idEl.ToString() : null;
        if (string.IsNullOrWhiteSpace(orderId))
        {
            return new ProviderCreateIntentResult(false, null, null, null, false, "Paymob order response missed id.");
        }

        using var keyResp = await httpClient.PostAsJsonAsync(
            BuildUri(paymob.BaseUrl, "acceptance/payment_keys"),
            new
            {
                auth_token = token,
                amount_cents = amountCents.ToString(CultureInfo.InvariantCulture),
                expiration = 3600,
                order_id = orderId,
                currency = request.Currency.Trim().ToUpperInvariant(),
                integration_id = int.TryParse(paymob.IntegrationId, out var intId) ? intId : 0,
                billing_data = new
                {
                    apartment = "NA",
                    email = "resident@propertymanagement.local",
                    floor = "NA",
                    first_name = "Tenant",
                    street = "NA",
                    building = "NA",
                    phone_number = "+201000000000",
                    shipping_method = "NA",
                    postal_code = "NA",
                    city = "Cairo",
                    country = "EG",
                    last_name = "Resident",
                    state = "NA"
                }
            },
            cancellationToken);

        if (!keyResp.IsSuccessStatusCode)
        {
            return new ProviderCreateIntentResult(false, orderId, null, null, false, $"Paymob payment key creation failed ({(int)keyResp.StatusCode}).");
        }

        var keyJson = await keyResp.Content.ReadAsStringAsync(cancellationToken);
        using var keyDoc = JsonDocument.Parse(keyJson);
        var paymentToken = keyDoc.RootElement.TryGetProperty("token", out var tokEl) ? tokEl.GetString() : null;
        var actionUrl = !string.IsNullOrWhiteSpace(paymob.IframeId) && !string.IsNullOrWhiteSpace(paymentToken)
            ? $"{paymob.BaseUrl.TrimEnd('/')}/acceptance/iframes/{paymob.IframeId}?payment_token={paymentToken}"
            : null;

        return new ProviderCreateIntentResult(
            Succeeded: !string.IsNullOrWhiteSpace(paymentToken),
            ProviderIntentId: orderId,
            ClientSecret: paymentToken,
            ActionUrl: actionUrl,
            RequiresAction: true);
    }

    public async Task<ProviderPaymentResult> CreateOrCapturePaymentAsync(
        ProviderPaymentRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(request.ProviderIntentId))
        {
            return await InquiryByOrderAsync(request.ProviderIntentId, cancellationToken);
        }

        return new ProviderPaymentResult(
            CallSucceeded: true,
            IsPaid: false,
            IsProcessing: true,
            ProviderPaymentId: request.ProviderIntentId ?? $"paymob_ord_{request.PaymentId:N}",
            ProviderCustomerId: null,
            ProviderTransactionId: null,
            ProviderStatus: "awaiting_callback",
            ProviderFee: null);
    }

    public Task<ProviderPaymentResult> GetPaymentStatusAsync(
        string providerPaymentId,
        CancellationToken cancellationToken = default) =>
        InquiryByOrderAsync(providerPaymentId, cancellationToken);

    public async Task<ProviderRefundResult> RefundPaymentAsync(
        ProviderRefundRequest request,
        CancellationToken cancellationToken = default)
    {
        var paymob = options.Value.Paymob;
        if (string.IsNullOrWhiteSpace(paymob.ApiKey))
        {
            return new ProviderRefundResult(false, false, null, null, "Paymob ApiKey is not configured.");
        }

        var token = await AuthenticateAsync(paymob, cancellationToken);
        if (string.IsNullOrWhiteSpace(token))
        {
            return new ProviderRefundResult(false, false, null, null, "Paymob authentication failed.");
        }

        var amountCents = (long)Math.Round(request.Amount * 100m, MidpointRounding.AwayFromZero);
        using var resp = await httpClient.PostAsJsonAsync(
            BuildUri(paymob.BaseUrl, "acceptance/void_refund/refund"),
            new
            {
                auth_token = token,
                transaction_id = request.ProviderPaymentId,
                amount_cents = amountCents
            },
            cancellationToken);

        var body = await resp.Content.ReadAsStringAsync(cancellationToken);
        if (!resp.IsSuccessStatusCode)
        {
            return new ProviderRefundResult(false, false, null, "failed", $"Paymob refund failed ({(int)resp.StatusCode}).");
        }

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        var success = GetBool(root, "success") ?? false;
        var refundTxId = root.TryGetProperty("id", out var idEl) ? idEl.ToString() : $"paymob_ref_{request.RefundId:N}";
        return new ProviderRefundResult(
            CallSucceeded: success,
            IsCompleted: success,
            ProviderRefundId: refundTxId,
            ProviderStatus: success ? "refunded" : "failed",
            FailureReason: success ? null : "Paymob declined the refund.");
    }

    public Task<IReadOnlyList<ProviderTransactionSnapshot>> FetchSettlementReportAsync(
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<ProviderTransactionSnapshot>>([]);
    }

    public ProviderWebhookVerificationResult VerifyAndParseWebhook(
        string rawBody,
        IReadOnlyDictionary<string, string> headers,
        DateTime utcNow)
    {
        var hmacSecret = options.Value.Paymob.HmacSecret;
        if (string.IsNullOrWhiteSpace(hmacSecret))
        {
            return InvalidSignature(utcNow, "Paymob HmacSecret is not configured.");
        }

        if (!TryGetHeader(headers, "hmac", out var providedHmac) &&
            !TryGetHeader(headers, "X-Paymob-Hmac", out providedHmac))
        {
            return InvalidSignature(utcNow, "Missing Paymob hmac header.");
        }

        try
        {
            using var doc = JsonDocument.Parse(rawBody);
            var root = doc.RootElement;
            var obj = root.TryGetProperty("obj", out var innerObj) && innerObj.ValueKind == JsonValueKind.Object
                ? innerObj
                : root;

            var concatenated = BuildPaymobHmacString(obj);
            using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(hmacSecret));
            var expectedHex = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(concatenated))).ToLowerInvariant();
            if (!FixedTimeHexEquals(providedHmac.Trim(), expectedHex))
            {
                return InvalidSignature(utcNow, "Paymob webhook HMAC verification failed.");
            }

            var createdAtStr = GetScalarString(obj, "created_at");
            var eventTime = DateTimeOffset.TryParse(createdAtStr, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dto)
                ? dto.UtcDateTime
                : utcNow;

            var tolerance = Math.Max(30, options.Value.WebhookToleranceSeconds);
            if (Math.Abs((utcNow - eventTime).TotalSeconds) > tolerance)
            {
                return new ProviderWebhookVerificationResult(
                    true, false, "Paymob webhook timestamp is outside the accepted replay tolerance window.",
                    null, null, eventTime, null, null, null, null, null, null, null, null, null);
            }

            var txId = GetScalarString(obj, "id");
            var orderId = obj.TryGetProperty("order", out var orderEl) && orderEl.ValueKind == JsonValueKind.Object
                ? GetScalarString(orderEl, "id")
                : null;
            var success = GetBool(obj, "success") ?? false;
            var pending = GetBool(obj, "pending") ?? false;
            var isRefunded = GetBool(obj, "is_refunded") ?? false;
            var isVoided = GetBool(obj, "is_voided") ?? false;
            var amountCents = long.TryParse(GetScalarString(obj, "amount_cents"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var ac) ? ac : 0;
            var currency = GetScalarString(obj, "currency")?.ToUpperInvariant();

            string eventType;
            string providerStatus;
            if (isRefunded || isVoided)
            {
                eventType = "refund.succeeded";
                providerStatus = isRefunded ? "refunded" : "voided";
            }
            else if (success && !pending)
            {
                eventType = "payment.succeeded";
                providerStatus = "succeeded";
            }
            else if (!success && !pending)
            {
                eventType = "payment.failed";
                providerStatus = "failed";
            }
            else
            {
                eventType = "payment.pending";
                providerStatus = "pending";
            }

            var eventId = !string.IsNullOrWhiteSpace(txId) ? $"paymob:{txId}:{eventType}" : null;

            return new ProviderWebhookVerificationResult(
                true, true, null, eventId, eventType, eventTime,
                ProviderPaymentId: orderId ?? txId,
                ProviderIntentId: orderId,
                ProviderTransactionId: txId,
                ProviderRefundId: isRefunded ? txId : null,
                Amount: MoneyRounding.Round(amountCents / 100m),
                FeeAmount: null,
                Currency: currency,
                ProviderStatus: providerStatus,
                FailureReason: !success && !pending ? "Paymob transaction failed." : null);
        }
        catch (JsonException ex)
        {
            return new ProviderWebhookVerificationResult(
                true, true, $"Invalid Paymob webhook payload: {ex.Message}",
                null, null, utcNow, null, null, null, null, null, null, null, null, null);
        }
    }

    private async Task<ProviderPaymentResult> InquiryByOrderAsync(string providerPaymentId, CancellationToken cancellationToken)
    {
        var paymob = options.Value.Paymob;
        if (string.IsNullOrWhiteSpace(paymob.ApiKey))
        {
            return new ProviderPaymentResult(false, false, false, providerPaymentId, null, null, null, null, "Paymob ApiKey is not configured.");
        }

        var token = await AuthenticateAsync(paymob, cancellationToken);
        if (string.IsNullOrWhiteSpace(token))
        {
            return new ProviderPaymentResult(false, false, true, providerPaymentId, null, null, null, null, "Paymob authentication failed.");
        }

        using var req = new HttpRequestMessage(HttpMethod.Get, BuildUri(paymob.BaseUrl, $"acceptance/transactions/{Uri.EscapeDataString(providerPaymentId)}"));
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var resp = await httpClient.SendAsync(req, cancellationToken);
        var body = await resp.Content.ReadAsStringAsync(cancellationToken);
        if (!resp.IsSuccessStatusCode)
        {
            return new ProviderPaymentResult(false, false, true, providerPaymentId, null, null, "pending", null, $"Paymob status check failed ({(int)resp.StatusCode}).");
        }

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        var success = GetBool(root, "success") ?? false;
        var pending = GetBool(root, "pending") ?? false;
        var txId = GetScalarString(root, "id");
        var orderId = root.TryGetProperty("order", out var orderEl) && orderEl.ValueKind == JsonValueKind.Object
            ? GetScalarString(orderEl, "id")
            : providerPaymentId;

        return new ProviderPaymentResult(
            CallSucceeded: success || pending,
            IsPaid: success && !pending,
            IsProcessing: pending,
            ProviderPaymentId: orderId ?? providerPaymentId,
            ProviderCustomerId: null,
            ProviderTransactionId: txId,
            ProviderStatus: success && !pending ? "succeeded" : pending ? "pending" : "failed",
            ProviderFee: null,
            FailureReason: !success && !pending ? "Paymob reported payment failure." : null);
    }

    private async Task<string?> AuthenticateAsync(PaymobProviderOptions paymob, CancellationToken cancellationToken)
    {
        using var resp = await httpClient.PostAsJsonAsync(
            BuildUri(paymob.BaseUrl, "auth/tokens"),
            new { api_key = paymob.ApiKey },
            cancellationToken);
        if (!resp.IsSuccessStatusCode) return null;
        var body = await resp.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.TryGetProperty("token", out var tok) ? tok.GetString() : null;
    }

    private static string BuildPaymobHmacString(JsonElement obj)
    {
        var orderId = obj.TryGetProperty("order", out var ord) && ord.ValueKind == JsonValueKind.Object
            ? GetScalarString(ord, "id") ?? ""
            : "";
        var hasSource = obj.TryGetProperty("source_data", out var sd) && sd.ValueKind == JsonValueKind.Object;
        var pan = hasSource ? GetScalarString(sd, "pan") ?? "" : "";
        var subType = hasSource ? GetScalarString(sd, "sub_type") ?? "" : "";
        var sdType = hasSource ? GetScalarString(sd, "type") ?? "" : "";

        return string.Concat(
            GetScalarString(obj, "amount_cents") ?? "",
            GetScalarString(obj, "created_at") ?? "",
            GetScalarString(obj, "currency") ?? "",
            GetScalarString(obj, "error_occured") ?? "",
            GetScalarString(obj, "has_parent_transaction") ?? "",
            GetScalarString(obj, "id") ?? "",
            GetScalarString(obj, "integration_id") ?? "",
            GetScalarString(obj, "is_3d_secure") ?? "",
            GetScalarString(obj, "is_auth") ?? "",
            GetScalarString(obj, "is_capture") ?? "",
            GetScalarString(obj, "is_refunded") ?? "",
            GetScalarString(obj, "is_standalone_payment") ?? "",
            GetScalarString(obj, "is_voided") ?? "",
            orderId,
            GetScalarString(obj, "owner") ?? "",
            GetScalarString(obj, "pending") ?? "",
            pan,
            subType,
            sdType,
            GetScalarString(obj, "success") ?? "");
    }

    private static Uri BuildUri(string baseUrl, string relative) =>
        new($"{baseUrl.TrimEnd('/')}/{relative.TrimStart('/')}");

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

    private static bool FixedTimeHexEquals(string a, string b)
    {
        var bytesA = Encoding.UTF8.GetBytes(a.ToLowerInvariant());
        var bytesB = Encoding.UTF8.GetBytes(b.ToLowerInvariant());
        return bytesA.Length == bytesB.Length && CryptographicOperations.FixedTimeEquals(bytesA, bytesB);
    }

    private static ProviderWebhookVerificationResult InvalidSignature(DateTime utcNow, string message) =>
        new(false, false, message, null, null, utcNow, null, null, null, null, null, null, null, null, null);

    private static bool? GetBool(JsonElement el, string prop)
    {
        if (!el.TryGetProperty(prop, out var val)) return null;
        return val.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null
        };
    }

    private static string? GetScalarString(JsonElement el, string prop)
    {
        if (!el.TryGetProperty(prop, out var val)) return null;
        return val.ValueKind switch
        {
            JsonValueKind.String => val.GetString(),
            JsonValueKind.Number => val.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null
        };
    }
}
