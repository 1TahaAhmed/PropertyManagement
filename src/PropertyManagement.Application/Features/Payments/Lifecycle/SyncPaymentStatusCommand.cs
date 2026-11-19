using System.Data;
using FluentValidation;
using MediatR;
using PropertyManagement.Application.Abstractions;
using PropertyManagement.Application.Abstractions.Audit;
using PropertyManagement.Application.Abstractions.Billing;
using PropertyManagement.Application.Abstractions.Ledger;
using PropertyManagement.Application.Abstractions.Payments;
using PropertyManagement.Application.Common.Enums;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Application.Features.Payments.Common;
using PropertyManagement.Application.Features.Payments.Responses;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Application.Features.Payments.Lifecycle;

public sealed record SyncPaymentStatusCommand(
    Guid OrganizationId,
    Guid ActorUserId,
    Guid PaymentId,
    bool AutoAllocate = true) : IRequest<Result<PaymentResponse>>;

public sealed class SyncPaymentStatusCommandValidator : AbstractValidator<SyncPaymentStatusCommand>
{
    public SyncPaymentStatusCommandValidator()
    {
        RuleFor(x => x.OrganizationId).NotEmpty();
        RuleFor(x => x.ActorUserId).NotEmpty();
        RuleFor(x => x.PaymentId).NotEmpty();
    }
}

public sealed class SyncPaymentStatusCommandHandler(
    IPaymentRepository paymentRepository,
    IInvoiceRepository invoiceRepository,
    ILedgerRepository ledgerRepository,
    IJournalEntryNumberGenerator journalEntryNumberGenerator,
    IFinancialAuditLogRepository auditRepository,
    IPaymentProviderFactory providerFactory,
    IUnitOfWork unitOfWork,
    IClock clock) : IRequestHandler<SyncPaymentStatusCommand, Result<PaymentResponse>>
{
    public async Task<Result<PaymentResponse>> Handle(SyncPaymentStatusCommand request, CancellationToken cancellationToken)
    {
        await using var transaction = await unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        await paymentRepository.AcquirePaymentLockAsync(request.PaymentId, request.OrganizationId, cancellationToken);

        var payment = await paymentRepository.GetByIdAsync(request.PaymentId, request.OrganizationId, cancellationToken);
        if (payment is null || payment.OrganizationId != request.OrganizationId)
        {
            return Result<PaymentResponse>.Failure(new Error("Payment.NotFound", "The specified payment was not found.", Type: ErrorType.NotFound));
        }

        if (payment.Status is not (PaymentStatus.Pending or PaymentStatus.Processing))
        {
            return Result<PaymentResponse>.Success(PaymentResponse.FromEntity(payment));
        }

        if (string.IsNullOrWhiteSpace(payment.ProviderPaymentId))
        {
            return Result<PaymentResponse>.Failure(new Error("Payment.NoExternalId", "Payment does not have an external provider payment ID to poll.", Type: ErrorType.Validation));
        }

        var provider = providerFactory.Resolve(payment.Provider);
        var statusResult = await provider.GetPaymentStatusAsync(payment.ProviderPaymentId, cancellationToken);
        var utcNow = clock.UtcNow;

        if (!statusResult.CallSucceeded)
        {
            if (!statusResult.IsProcessing)
            {
                payment.MarkFailed(statusResult.FailureReason ?? "Provider reported payment failure.", utcNow, statusResult.ProviderStatus);
            }
        }
        else if (statusResult.IsPaid)
        {
            payment.MarkSucceeded(
                utcNow,
                providerTransactionId: statusResult.ProviderTransactionId,
                providerStatus: statusResult.ProviderStatus,
                providerFee: statusResult.ProviderFee,
                receivedAtUtc: utcNow);

            if (request.AutoAllocate && payment.OverpaymentPolicy != OverpaymentPolicy.RejectOverpayment)
            {
                await PaymentWorkflowHelper.ApplyAllocationsUnderLockAsync(
                    payment,
                    AllocationStrategy.OldestOverdueFirst,
                    null,
                    invoiceRepository,
                    utcNow,
                    request.ActorUserId,
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
        else if (statusResult.IsProcessing)
        {
            payment.MarkProcessing(statusResult.ProviderPaymentId, statusResult.ProviderStatus, utcNow);
        }

        await PaymentWorkflowHelper.RecordAuditAsync(
            auditRepository,
            request.OrganizationId,
            "Payment.StatusSynced",
            "Payment",
            payment.Id,
            new { NewStatus = payment.Status.ToString(), statusResult.ProviderStatus },
            utcNow,
            request.ActorUserId,
            cancellationToken: cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result<PaymentResponse>.Success(PaymentResponse.FromEntity(payment));
    }
}
