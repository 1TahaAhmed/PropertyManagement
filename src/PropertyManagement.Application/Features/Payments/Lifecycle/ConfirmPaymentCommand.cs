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
using PropertyManagement.Domain.Entities.Payments.Services;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Application.Features.Payments.Lifecycle;

public sealed record ConfirmPaymentCommand(
    Guid OrganizationId,
    Guid ActorUserId,
    Guid PaymentId,
    string? ProviderTransactionId = null,
    AllocationStrategy AllocationStrategy = AllocationStrategy.Explicit,
    IReadOnlyList<RequestedInvoiceAllocation>? Allocations = null,
    string? RowVersion = null) : IRequest<Result<PaymentResponse>>;

public sealed class ConfirmPaymentCommandValidator : AbstractValidator<ConfirmPaymentCommand>
{
    public ConfirmPaymentCommandValidator()
    {
        RuleFor(x => x.OrganizationId).NotEmpty();
        RuleFor(x => x.ActorUserId).NotEmpty();
        RuleFor(x => x.PaymentId).NotEmpty();
        RuleFor(x => x.AllocationStrategy).IsInEnum();
    }
}

public sealed class ConfirmPaymentCommandHandler(
    IPaymentRepository paymentRepository,
    IInvoiceRepository invoiceRepository,
    ILedgerRepository ledgerRepository,
    IJournalEntryNumberGenerator journalEntryNumberGenerator,
    IFinancialAuditLogRepository auditRepository,
    IUnitOfWork unitOfWork,
    IClock clock) : IRequestHandler<ConfirmPaymentCommand, Result<PaymentResponse>>
{
    public async Task<Result<PaymentResponse>> Handle(ConfirmPaymentCommand request, CancellationToken cancellationToken)
    {
        await using var transaction = await unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        await paymentRepository.AcquirePaymentLockAsync(request.PaymentId, request.OrganizationId, cancellationToken);

        var payment = await paymentRepository.GetByIdAsync(request.PaymentId, request.OrganizationId, cancellationToken);
        if (payment is null || payment.OrganizationId != request.OrganizationId)
        {
            return Result<PaymentResponse>.Failure(new Error("Payment.NotFound", "The specified payment was not found.", Type: ErrorType.NotFound));
        }

        if (!string.IsNullOrWhiteSpace(request.RowVersion))
        {
            try
            {
                paymentRepository.SetRowVersion(payment, Convert.FromBase64String(request.RowVersion));
            }
            catch (FormatException)
            {
                return Result<PaymentResponse>.Failure(new Error("Payment.InvalidRowVersion", "The provided RowVersion concurrency token is invalid.", Type: ErrorType.Validation));
            }
        }

        if (payment.Status is not (PaymentStatus.Pending or PaymentStatus.Processing))
        {
            return Result<PaymentResponse>.Failure(new Error("Payment.InvalidState", $"Cannot confirm payment in '{payment.Status}' status.", Type: ErrorType.Conflict));
        }

        var utcNow = clock.UtcNow;
        try
        {
            payment.MarkSucceeded(
                utcNow,
                providerTransactionId: request.ProviderTransactionId,
                providerStatus: "confirmed",
                receivedAtUtc: utcNow,
                confirmedByUserId: request.ActorUserId);

            if ((request.Allocations is { Count: > 0 }) || request.AllocationStrategy != AllocationStrategy.Explicit)
            {
                await PaymentWorkflowHelper.ApplyAllocationsUnderLockAsync(
                    payment,
                    request.AllocationStrategy,
                    request.Allocations,
                    invoiceRepository,
                    utcNow,
                    request.ActorUserId,
                    cancellationToken);
            }
            else
            {
                payment.RecordUnappliedCreditNotice();
            }

            await PaymentWorkflowHelper.PostReceiptLedgerIfMissingAsync(
                payment,
                payment.AllocatedAmount,
                ledgerRepository,
                journalEntryNumberGenerator,
                utcNow,
                cancellationToken);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return Result<PaymentResponse>.Failure(new Error("Payment.ConfirmError", ex.Message, Type: ErrorType.Validation));
        }

        await PaymentWorkflowHelper.RecordAuditAsync(
            auditRepository,
            request.OrganizationId,
            "Payment.Confirmed",
            "Payment",
            payment.Id,
            new { payment.AllocatedAmount, payment.UnappliedAmount },
            utcNow,
            request.ActorUserId,
            cancellationToken: cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result<PaymentResponse>.Success(PaymentResponse.FromEntity(payment));
    }
}
