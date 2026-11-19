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
using PropertyManagement.Domain.Entities.Ledger.Services;

namespace PropertyManagement.Application.Features.Payments.Allocations;

public sealed record ReversePaymentAllocationCommand(
    Guid OrganizationId,
    Guid ActorUserId,
    Guid PaymentId,
    Guid AllocationId,
    string Reason,
    string? RowVersion = null) : IRequest<Result<PaymentResponse>>;

public sealed class ReversePaymentAllocationCommandValidator : AbstractValidator<ReversePaymentAllocationCommand>
{
    public ReversePaymentAllocationCommandValidator()
    {
        RuleFor(x => x.OrganizationId).NotEmpty();
        RuleFor(x => x.ActorUserId).NotEmpty();
        RuleFor(x => x.PaymentId).NotEmpty();
        RuleFor(x => x.AllocationId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}

public sealed class ReversePaymentAllocationCommandHandler(
    IPaymentRepository paymentRepository,
    IInvoiceRepository invoiceRepository,
    ILedgerRepository ledgerRepository,
    IJournalEntryNumberGenerator journalEntryNumberGenerator,
    IFinancialAuditLogRepository auditRepository,
    IUnitOfWork unitOfWork,
    IClock clock) : IRequestHandler<ReversePaymentAllocationCommand, Result<PaymentResponse>>
{
    public async Task<Result<PaymentResponse>> Handle(ReversePaymentAllocationCommand request, CancellationToken cancellationToken)
    {
        await using var transaction = await unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        await paymentRepository.AcquirePaymentLockAsync(request.PaymentId, request.OrganizationId, cancellationToken);

        var initialPayment = await paymentRepository.GetByIdAsync(request.PaymentId, request.OrganizationId, cancellationToken);
        if (initialPayment is null || initialPayment.OrganizationId != request.OrganizationId)
        {
            return Result<PaymentResponse>.Failure(new Error("Payment.NotFound", "The specified payment was not found.", Type: ErrorType.NotFound));
        }

        paymentRepository.Detach(initialPayment);
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

        var targetAllocation = payment.Allocations.FirstOrDefault(a => a.Id == request.AllocationId);
        if (targetAllocation is null)
        {
            return Result<PaymentResponse>.Failure(new Error("PaymentAllocation.NotFound", "The specified payment allocation was not found.", Type: ErrorType.NotFound));
        }

        if (targetAllocation.IsReversed)
        {
            return Result<PaymentResponse>.Failure(new Error("PaymentAllocation.AlreadyReversed", "This payment allocation has already been reversed.", Type: ErrorType.Conflict));
        }

        await invoiceRepository.AcquireInvoiceLockAsync(targetAllocation.InvoiceId, request.OrganizationId, cancellationToken);
        var invoice = await invoiceRepository.GetByIdAsync(targetAllocation.InvoiceId, request.OrganizationId, cancellationToken);
        if (invoice is null)
        {
            return Result<PaymentResponse>.Failure(new Error("Invoice.NotFound", "The linked invoice was not found.", Type: ErrorType.NotFound));
        }

        var utcNow = clock.UtcNow;
        try
        {
            var reversed = payment.ReverseAllocation(request.AllocationId, request.Reason, utcNow, request.ActorUserId);
            invoice.ReversePaymentAllocation(reversed.Amount, DateOnly.FromDateTime(utcNow), utcNow);
            invoiceRepository.Update(invoice);

            var reversalKey = $"alloc-rev:{reversed.Id}";
            if (!await ledgerRepository.ExistsByIdempotencyKeyAsync(
                    payment.OrganizationId, "PaymentAllocationReversed", reversed.Id, reversalKey, cancellationToken))
            {
                var jeNumber = await journalEntryNumberGenerator.GenerateNextAsync(payment.OrganizationId, utcNow, cancellationToken);
                var je = LedgerPostingService.CreateAllocationReversalEntry(payment, reversed, jeNumber, reversalKey, utcNow);
                await ledgerRepository.AddAsync(je, cancellationToken);
            }

            await PaymentWorkflowHelper.RecordAuditAsync(
                auditRepository,
                request.OrganizationId,
                "PaymentAllocation.Reversed",
                "PaymentAllocation",
                reversed.Id,
                new { PaymentId = payment.Id, InvoiceId = invoice.Id, reversed.Amount, request.Reason },
                utcNow,
                request.ActorUserId,
                cancellationToken: cancellationToken);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return Result<PaymentResponse>.Failure(new Error("PaymentAllocation.ReversalError", ex.Message, Type: ErrorType.Validation));
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result<PaymentResponse>.Success(PaymentResponse.FromEntity(payment));
    }
}
