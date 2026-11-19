using System.Data;
using FluentValidation;
using MediatR;
using PropertyManagement.Application.Abstractions;
using PropertyManagement.Application.Abstractions.Audit;
using PropertyManagement.Application.Abstractions.Ledger;
using PropertyManagement.Application.Abstractions.Payments;
using PropertyManagement.Application.Common.Enums;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Application.Features.Payments.Common;
using PropertyManagement.Application.Features.Payments.Responses;
using PropertyManagement.Domain.Entities.Ledger.Services;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Application.Features.Payments.Refunds;

public sealed record RefundPaymentCommand(
    Guid OrganizationId,
    Guid ActorUserId,
    Guid PaymentId,
    decimal Amount,
    string Reason,
    string? IdempotencyKey = null,
    string? RowVersion = null) : IRequest<Result<RefundResponse>>;

public sealed class RefundPaymentCommandValidator : AbstractValidator<RefundPaymentCommand>
{
    public RefundPaymentCommandValidator()
    {
        RuleFor(x => x.OrganizationId).NotEmpty();
        RuleFor(x => x.ActorUserId).NotEmpty();
        RuleFor(x => x.PaymentId).NotEmpty();
        RuleFor(x => x.Amount).GreaterThan(0m);
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
        RuleFor(x => x.IdempotencyKey).MaximumLength(150);
    }
}

public sealed class RefundPaymentCommandHandler(
    IPaymentRepository paymentRepository,
    ILedgerRepository ledgerRepository,
    IJournalEntryNumberGenerator journalEntryNumberGenerator,
    IFinancialAuditLogRepository auditRepository,
    IPaymentProviderFactory providerFactory,
    IUnitOfWork unitOfWork,
    IClock clock) : IRequestHandler<RefundPaymentCommand, Result<RefundResponse>>
{
    public async Task<Result<RefundResponse>> Handle(RefundPaymentCommand request, CancellationToken cancellationToken)
    {
        await using var transaction = await unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        await paymentRepository.AcquirePaymentLockAsync(request.PaymentId, request.OrganizationId, cancellationToken);

        var initialPayment = await paymentRepository.GetByIdAsync(request.PaymentId, request.OrganizationId, cancellationToken);
        if (initialPayment is null || initialPayment.OrganizationId != request.OrganizationId)
        {
            return Result<RefundResponse>.Failure(new Error("Payment.NotFound", "The specified payment was not found.", Type: ErrorType.NotFound));
        }

        paymentRepository.Detach(initialPayment);
        var payment = await paymentRepository.GetByIdAsync(request.PaymentId, request.OrganizationId, cancellationToken);
        if (payment is null || payment.OrganizationId != request.OrganizationId)
        {
            return Result<RefundResponse>.Failure(new Error("Payment.NotFound", "The specified payment was not found.", Type: ErrorType.NotFound));
        }

        if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            var existingRefund = payment.Refunds.FirstOrDefault(r =>
                string.Equals(r.IdempotencyKey, request.IdempotencyKey.Trim(), StringComparison.Ordinal));
            if (existingRefund is not null)
            {
                return Result<RefundResponse>.Success(RefundResponse.FromEntity(existingRefund));
            }
        }

        if (!string.IsNullOrWhiteSpace(request.RowVersion))
        {
            try
            {
                paymentRepository.SetRowVersion(payment, Convert.FromBase64String(request.RowVersion));
            }
            catch (FormatException)
            {
                return Result<RefundResponse>.Failure(new Error("Payment.InvalidRowVersion", "The provided RowVersion concurrency token is invalid.", Type: ErrorType.Validation));
            }
        }

        var utcNow = clock.UtcNow;
        Domain.Entities.Payments.Refund refund;
        try
        {
            refund = payment.InitiateRefund(
                request.Amount,
                request.Reason,
                utcNow,
                request.ActorUserId,
                request.IdempotencyKey);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return Result<RefundResponse>.Failure(new Error("Payment.RefundValidationError", ex.Message, Type: ErrorType.Validation));
        }

        if (payment.Provider == PaymentProviderType.Manual)
        {
            payment.CompleteRefund(refund.Id, null, utcNow);
        }
        else
        {
            if (string.IsNullOrWhiteSpace(payment.ProviderPaymentId))
            {
                payment.FailRefund(refund.Id, "Payment is missing external provider payment ID.", utcNow);
                await unitOfWork.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return Result<RefundResponse>.Failure(new Error("Payment.MissingProviderPaymentId", "Payment does not have an external provider payment ID to refund.", Type: ErrorType.Validation));
            }

            var provider = providerFactory.Resolve(payment.Provider);
            var providerResult = await provider.RefundPaymentAsync(
                new ProviderRefundRequest(
                    payment.OrganizationId,
                    payment.Id,
                    refund.Id,
                    payment.ProviderPaymentId,
                    refund.Amount,
                    refund.Currency,
                    refund.Reason,
                    request.IdempotencyKey ?? refund.Id.ToString("N")),
                cancellationToken);

            if (!providerResult.CallSucceeded)
            {
                payment.FailRefund(refund.Id, providerResult.FailureReason ?? "Provider refund failed.", utcNow);
                await PaymentWorkflowHelper.RecordAuditAsync(
                    auditRepository,
                    request.OrganizationId,
                    "Payment.RefundFailed",
                    "Refund",
                    refund.Id,
                    new { PaymentId = payment.Id, refund.Amount, Reason = providerResult.FailureReason },
                    utcNow,
                    request.ActorUserId,
                    request.IdempotencyKey,
                    cancellationToken);

                await unitOfWork.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return Result<RefundResponse>.Failure(new Error("Payment.ProviderRefundFailed", providerResult.FailureReason ?? "Payment provider rejected the refund.", Type: ErrorType.Failure));
            }

            if (providerResult.IsCompleted)
            {
                payment.CompleteRefund(refund.Id, providerResult.ProviderRefundId, utcNow);
            }
            else
            {
                payment.MarkRefundProcessing(refund.Id, providerResult.ProviderRefundId, utcNow);
            }
        }

        if (refund.Status == RefundStatus.Succeeded)
        {
            var refundKey = $"refund:{refund.Id}";
            if (!await ledgerRepository.ExistsByIdempotencyKeyAsync(
                    payment.OrganizationId, "PaymentRefunded", refund.Id, refundKey, cancellationToken))
            {
                var jeNumber = await journalEntryNumberGenerator.GenerateNextAsync(payment.OrganizationId, utcNow, cancellationToken);
                var je = LedgerPostingService.CreateRefundEntry(payment, refund, jeNumber, refundKey, utcNow);
                await ledgerRepository.AddAsync(je, cancellationToken);
            }
        }

        await PaymentWorkflowHelper.RecordAuditAsync(
            auditRepository,
            request.OrganizationId,
            $"Refund.{refund.Status}",
            "Refund",
            refund.Id,
            new { PaymentId = payment.Id, refund.Amount, refund.Currency, refund.Reason, refund.ProviderRefundId },
            utcNow,
            request.ActorUserId,
            request.IdempotencyKey,
            cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result<RefundResponse>.Success(RefundResponse.FromEntity(refund));
    }
}
