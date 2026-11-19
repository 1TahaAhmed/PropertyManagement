using System.Data;
using System.Security.Cryptography;
using System.Text;
using FluentValidation;
using MediatR;
using PropertyManagement.Application.Abstractions;
using PropertyManagement.Application.Abstractions.Audit;
using PropertyManagement.Application.Abstractions.Billing;
using PropertyManagement.Application.Abstractions.Ledger;
using PropertyManagement.Application.Abstractions.Payments;
using PropertyManagement.Application.Abstractions.Security;
using PropertyManagement.Application.Common.Enums;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Application.Features.Payments.Common;
using PropertyManagement.Application.Features.Payments.Responses;
using PropertyManagement.Domain.Entities.Ledger.Services;
using PropertyManagement.Domain.Entities.Payments;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Application.Features.Payments.Webhooks;

public sealed record ProcessPaymentWebhookCommand(
    PaymentProviderType Provider,
    string RawBody,
    IReadOnlyDictionary<string, string> Headers) : IRequest<Result<WebhookProcessingResponse>>;

public sealed class ProcessPaymentWebhookCommandValidator : AbstractValidator<ProcessPaymentWebhookCommand>
{
    public ProcessPaymentWebhookCommandValidator()
    {
        RuleFor(x => x.Provider).IsInEnum();
        RuleFor(x => x.RawBody).NotEmpty();
    }
}

public sealed class ProcessPaymentWebhookCommandHandler(
    IPaymentProviderFactory providerFactory,
    IWebhookEventRepository webhookEventRepository,
    IPaymentRepository paymentRepository,
    IPaymentIntentRepository intentRepository,
    IInvoiceRepository invoiceRepository,
    ILedgerRepository ledgerRepository,
    IJournalEntryNumberGenerator journalEntryNumberGenerator,
    IFinancialAuditLogRepository auditRepository,
    ISystemTenantContext systemTenantContext,
    IUnitOfWork unitOfWork,
    IClock clock) : IRequestHandler<ProcessPaymentWebhookCommand, Result<WebhookProcessingResponse>>
{
    public async Task<Result<WebhookProcessingResponse>> Handle(ProcessPaymentWebhookCommand request, CancellationToken cancellationToken)
    {
        var utcNow = clock.UtcNow;
        var provider = providerFactory.Resolve(request.Provider);
        var verification = provider.VerifyAndParseWebhook(request.RawBody, request.Headers, utcNow);

        if (!verification.IsSignatureValid)
        {
            return Result<WebhookProcessingResponse>.Failure(new Error("Webhook.InvalidSignature", verification.ErrorMessage ?? "Webhook signature verification failed.", Type: ErrorType.Unauthorized));
        }

        if (!verification.IsWithinReplayWindow)
        {
            return Result<WebhookProcessingResponse>.Failure(new Error("Webhook.ReplayRejected", verification.ErrorMessage ?? "Webhook timestamp is outside the accepted replay tolerance window.", Type: ErrorType.Validation));
        }

        if (string.IsNullOrWhiteSpace(verification.EventId) || string.IsNullOrWhiteSpace(verification.EventType))
        {
            return Result<WebhookProcessingResponse>.Failure(new Error("Webhook.InvalidPayload", "Webhook payload is missing event ID or event type.", Type: ErrorType.Validation));
        }

        var existingEvent = await webhookEventRepository.GetByProviderEventIdAsync(
            request.Provider, verification.EventId, cancellationToken);
        if (existingEvent is not null)
        {
            return Result<WebhookProcessingResponse>.Success(new WebhookProcessingResponse(
                verification.EventId,
                request.Provider,
                WebhookEventStatus.IgnoredDuplicate,
                IsDuplicate: true,
                PaymentId: null,
                Message: "Webhook event has already been processed."));
        }

        var payloadHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(request.RawBody)));
        var webhookEvent = ProcessedWebhookEvent.CreateReceived(
            request.Provider,
            verification.EventId,
            verification.EventType,
            payloadHash,
            verification.EventTimestampUtc,
            utcNow);

        await using var transaction = await unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        // Locate target payment ignoring tenant filters
        Payment? payment = null;
        if (!string.IsNullOrWhiteSpace(verification.ProviderPaymentId))
        {
            payment = await paymentRepository.GetByProviderPaymentIdAsync(
                request.Provider, verification.ProviderPaymentId, cancellationToken);
        }

        if (payment is null && !string.IsNullOrWhiteSpace(verification.ProviderTransactionId))
        {
            payment = await paymentRepository.GetByProviderTransactionIdAsync(
                request.Provider, verification.ProviderTransactionId, cancellationToken);
        }

        PaymentIntent? intent = null;
        if (!string.IsNullOrWhiteSpace(verification.ProviderIntentId))
        {
            intent = await intentRepository.GetByProviderIntentIdAsync(verification.ProviderIntentId, cancellationToken);
        }

        if (payment is null)
        {
            webhookEvent.MarkFailed("Matching internal payment was not found.", utcNow, intent?.OrganizationId);
            await webhookEventRepository.AddAsync(webhookEvent, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Result<WebhookProcessingResponse>.Failure(new Error("Webhook.PaymentNotFound", "No internal payment matched the webhook identifiers.", Type: ErrorType.NotFound));
        }

        using var tenantScope = systemTenantContext.BeginScope(payment.OrganizationId);

        await paymentRepository.AcquirePaymentLockAsync(payment.Id, payment.OrganizationId, cancellationToken);
        paymentRepository.Detach(payment);
        payment = await paymentRepository.GetByIdAsync(payment.Id, payment.OrganizationId, cancellationToken)
                  ?? throw new InvalidOperationException("Payment disappeared during webhook lock.");

        var normalizedEventType = verification.EventType.Trim().ToLowerInvariant();
        if (normalizedEventType is "payment.succeeded" or "payment_intent.succeeded" or "charge.succeeded")
        {
            if (payment.Status is PaymentStatus.Pending or PaymentStatus.Processing)
            {
                payment.MarkSucceeded(
                    utcNow,
                    providerTransactionId: verification.ProviderTransactionId,
                    providerStatus: verification.ProviderStatus ?? "succeeded",
                    providerFee: verification.FeeAmount,
                    receivedAtUtc: verification.EventTimestampUtc);

                if (intent is not null && intent.Status is PaymentIntentStatus.Pending or PaymentIntentStatus.RequiresAction or PaymentIntentStatus.Processing)
                {
                    intent.MarkSucceeded(utcNow);
                }

                if (payment.OverpaymentPolicy == OverpaymentPolicy.AutoApplyToOldestInvoices)
                {
                    await PaymentWorkflowHelper.ApplyAllocationsUnderLockAsync(
                        payment,
                        AllocationStrategy.OldestOverdueFirst,
                        null,
                        invoiceRepository,
                        utcNow,
                        actorUserId: null,
                        cancellationToken);
                }

                await PaymentWorkflowHelper.PostReceiptLedgerIfMissingAsync(
                    payment,
                    payment.AllocatedAmount,
                    ledgerRepository,
                    journalEntryNumberGenerator,
                    utcNow,
                    cancellationToken);
            }
        }
        else if (normalizedEventType is "payment.failed" or "payment_intent.payment_failed" or "charge.failed")
        {
            // Protect against out-of-order stale failed webhook arriving after Succeeded
            if (payment.Status is PaymentStatus.Pending or PaymentStatus.Processing)
            {
                payment.MarkFailed(
                    verification.FailureReason ?? "Payment failed at provider.",
                    utcNow,
                    verification.ProviderStatus ?? "failed");

                if (intent is not null && intent.Status is PaymentIntentStatus.Pending or PaymentIntentStatus.RequiresAction or PaymentIntentStatus.Processing)
                {
                    intent.MarkFailed(verification.FailureReason ?? "Payment failed at provider.", utcNow);
                }
            }
        }
        else if (normalizedEventType is "refund.succeeded" or "charge.refunded")
        {
            var pendingRefund = !string.IsNullOrWhiteSpace(verification.ProviderRefundId)
                ? payment.Refunds.FirstOrDefault(r => string.Equals(r.ProviderRefundId, verification.ProviderRefundId, StringComparison.Ordinal))
                : payment.Refunds.FirstOrDefault(r => r.Status is RefundStatus.Pending or RefundStatus.Processing);

            if (pendingRefund is not null && pendingRefund.Status != RefundStatus.Succeeded)
            {
                payment.CompleteRefund(pendingRefund.Id, verification.ProviderRefundId, utcNow);
                var refundKey = $"refund:{pendingRefund.Id}";
                if (!await ledgerRepository.ExistsByIdempotencyKeyAsync(
                        payment.OrganizationId, "PaymentRefunded", pendingRefund.Id, refundKey, cancellationToken))
                {
                    var jeNumber = await journalEntryNumberGenerator.GenerateNextAsync(payment.OrganizationId, utcNow, cancellationToken);
                    var je = LedgerPostingService.CreateRefundEntry(payment, pendingRefund, jeNumber, refundKey, utcNow);
                    await ledgerRepository.AddAsync(je, cancellationToken);
                }
            }
        }
        else
        {
            webhookEvent.MarkFailed($"Unsupported webhook event type '{verification.EventType}'.", utcNow, payment.OrganizationId);
            await webhookEventRepository.AddAsync(webhookEvent, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Result<WebhookProcessingResponse>.Failure(new Error("Webhook.UnsupportedEventType", $"Unsupported webhook event type '{verification.EventType}'.", Type: ErrorType.Validation));
        }

        webhookEvent.MarkProcessed(utcNow, payment.OrganizationId);
        await webhookEventRepository.AddAsync(webhookEvent, cancellationToken);

        await PaymentWorkflowHelper.RecordAuditAsync(
            auditRepository,
            payment.OrganizationId,
            $"Webhook.{verification.EventType}",
            "Payment",
            payment.Id,
            new { verification.EventId, PaymentStatus = payment.Status.ToString() },
            utcNow,
            actorUserId: null,
            correlationId: verification.EventId,
            cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result<WebhookProcessingResponse>.Success(new WebhookProcessingResponse(
            verification.EventId,
            request.Provider,
            WebhookEventStatus.Processed,
            IsDuplicate: false,
            PaymentId: payment.Id,
            Message: "Webhook processed successfully."));
    }
}
