using PropertyManagement.Domain.Entities.Payments.Services;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Application.Abstractions.Payments;

public sealed record ProviderCreateIntentRequest(
    Guid OrganizationId,
    Guid IntentId,
    Guid ResidentId,
    decimal Amount,
    string Currency,
    PaymentMethod PaymentMethod,
    string IdempotencyKey,
    string? Description = null);

public sealed record ProviderCreateIntentResult(
    bool Succeeded,
    string? ProviderIntentId,
    string? ClientSecret,
    string? ActionUrl,
    bool RequiresAction,
    string? ErrorMessage = null);

public sealed record ProviderPaymentRequest(
    Guid OrganizationId,
    Guid PaymentId,
    Guid ResidentId,
    decimal Amount,
    string Currency,
    PaymentMethod PaymentMethod,
    string IdempotencyKey,
    string? ProviderIntentId = null,
    string? Reference = null);

public sealed record ProviderPaymentResult(
    bool CallSucceeded,
    bool IsPaid,
    bool IsProcessing,
    string? ProviderPaymentId,
    string? ProviderCustomerId,
    string? ProviderTransactionId,
    string? ProviderStatus,
    decimal? ProviderFee,
    string? FailureReason = null);

public sealed record ProviderRefundRequest(
    Guid OrganizationId,
    Guid PaymentId,
    Guid RefundId,
    string ProviderPaymentId,
    decimal Amount,
    string Currency,
    string Reason,
    string IdempotencyKey);

public sealed record ProviderRefundResult(
    bool CallSucceeded,
    bool IsCompleted,
    string? ProviderRefundId,
    string? ProviderStatus,
    string? FailureReason = null);

public sealed record ProviderWebhookVerificationResult(
    bool IsSignatureValid,
    bool IsWithinReplayWindow,
    string? ErrorMessage,
    string? EventId,
    string? EventType,
    DateTime EventTimestampUtc,
    string? ProviderPaymentId,
    string? ProviderIntentId,
    string? ProviderTransactionId,
    string? ProviderRefundId,
    decimal? Amount,
    decimal? FeeAmount,
    string? Currency,
    string? ProviderStatus,
    string? FailureReason);

public interface IPaymentProvider
{
    PaymentProviderType ProviderType { get; }

    Task<ProviderCreateIntentResult> CreateIntentAsync(
        ProviderCreateIntentRequest request,
        CancellationToken cancellationToken = default);

    Task<ProviderPaymentResult> CreateOrCapturePaymentAsync(
        ProviderPaymentRequest request,
        CancellationToken cancellationToken = default);

    Task<ProviderPaymentResult> GetPaymentStatusAsync(
        string providerPaymentId,
        CancellationToken cancellationToken = default);

    Task<ProviderRefundResult> RefundPaymentAsync(
        ProviderRefundRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProviderTransactionSnapshot>> FetchSettlementReportAsync(
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default);

    ProviderWebhookVerificationResult VerifyAndParseWebhook(
        string rawBody,
        IReadOnlyDictionary<string, string> headers,
        DateTime utcNow);
}

public interface IPaymentProviderFactory
{
    IPaymentProvider Resolve(PaymentProviderType providerType);
}
