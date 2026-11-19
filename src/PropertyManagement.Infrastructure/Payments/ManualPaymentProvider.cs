using PropertyManagement.Application.Abstractions.Payments;
using PropertyManagement.Domain.Entities.Payments.Services;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Infrastructure.Payments;

public sealed class ManualPaymentProvider : IPaymentProvider
{
    public PaymentProviderType ProviderType => PaymentProviderType.Manual;

    public Task<ProviderCreateIntentResult> CreateIntentAsync(
        ProviderCreateIntentRequest request,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new ProviderCreateIntentResult(
            Succeeded: true,
            ProviderIntentId: $"manual_intent_{request.IntentId:N}",
            ClientSecret: null,
            ActionUrl: null,
            RequiresAction: false));
    }

    public Task<ProviderPaymentResult> CreateOrCapturePaymentAsync(
        ProviderPaymentRequest request,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new ProviderPaymentResult(
            CallSucceeded: true,
            IsPaid: false,
            IsProcessing: false,
            ProviderPaymentId: $"manual_pay_{request.PaymentId:N}",
            ProviderCustomerId: null,
            ProviderTransactionId: request.Reference,
            ProviderStatus: "manual_pending",
            ProviderFee: 0m));
    }

    public Task<ProviderPaymentResult> GetPaymentStatusAsync(
        string providerPaymentId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new ProviderPaymentResult(
            CallSucceeded: true,
            IsPaid: false,
            IsProcessing: false,
            ProviderPaymentId: providerPaymentId,
            ProviderCustomerId: null,
            ProviderTransactionId: null,
            ProviderStatus: "manual_pending",
            ProviderFee: 0m));
    }

    public Task<ProviderRefundResult> RefundPaymentAsync(
        ProviderRefundRequest request,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new ProviderRefundResult(
            CallSucceeded: true,
            IsCompleted: true,
            ProviderRefundId: $"manual_ref_{request.RefundId:N}",
            ProviderStatus: "manual_refunded"));
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
        return new ProviderWebhookVerificationResult(
            IsSignatureValid: false,
            IsWithinReplayWindow: false,
            ErrorMessage: "Manual payment provider does not accept external webhooks.",
            EventId: null,
            EventType: null,
            EventTimestampUtc: utcNow,
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
