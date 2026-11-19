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
using PropertyManagement.Domain.Entities.Payments.Services;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Application.Features.Payments.Allocations;

public sealed record AllocatePaymentCommand(
    Guid OrganizationId,
    Guid ActorUserId,
    Guid PaymentId,
    AllocationStrategy Strategy = AllocationStrategy.Explicit,
    IReadOnlyList<RequestedInvoiceAllocation>? Allocations = null,
    string? RowVersion = null) : IRequest<Result<PaymentResponse>>;

public sealed class AllocatePaymentCommandValidator : AbstractValidator<AllocatePaymentCommand>
{
    public AllocatePaymentCommandValidator()
    {
        RuleFor(x => x.OrganizationId).NotEmpty();
        RuleFor(x => x.ActorUserId).NotEmpty();
        RuleFor(x => x.PaymentId).NotEmpty();
        RuleFor(x => x.Strategy).IsInEnum();
        RuleForEach(x => x.Allocations).ChildRules(item =>
        {
            item.RuleFor(a => a.InvoiceId).NotEmpty();
            item.RuleFor(a => a.Amount).GreaterThan(0m);
        });
    }
}

public sealed class AllocatePaymentCommandHandler(
    IPaymentRepository paymentRepository,
    IInvoiceRepository invoiceRepository,
    ILedgerRepository ledgerRepository,
    IJournalEntryNumberGenerator journalEntryNumberGenerator,
    IFinancialAuditLogRepository auditRepository,
    IUnitOfWork unitOfWork,
    IClock clock) : IRequestHandler<AllocatePaymentCommand, Result<PaymentResponse>>
{
    public async Task<Result<PaymentResponse>> Handle(AllocatePaymentCommand request, CancellationToken cancellationToken)
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

        if (payment.Status is not (PaymentStatus.Succeeded or PaymentStatus.PartiallyRefunded))
        {
            return Result<PaymentResponse>.Failure(new Error("Payment.InvalidState", $"Cannot allocate payment in '{payment.Status}' status.", Type: ErrorType.Conflict));
        }

        var utcNow = clock.UtcNow;
        try
        {
            var createdAllocations = await PaymentWorkflowHelper.ApplyAllocationsUnderLockAsync(
                payment,
                request.Strategy,
                request.Allocations,
                invoiceRepository,
                utcNow,
                request.ActorUserId,
                cancellationToken);

            // Ensure receipt ledger exists first (for cases where payment was confirmed without receipt posting),
            // otherwise post individual allocation entries consuming unapplied customer credit.
            var receiptKey = $"receipt:{payment.Id}";
            if (!await ledgerRepository.ExistsByIdempotencyKeyAsync(
                    payment.OrganizationId, "PaymentSucceeded", payment.Id, receiptKey, cancellationToken))
            {
                await PaymentWorkflowHelper.PostReceiptLedgerIfMissingAsync(
                    payment,
                    payment.AllocatedAmount,
                    ledgerRepository,
                    journalEntryNumberGenerator,
                    utcNow,
                    cancellationToken);
            }
            else
            {
                foreach (var alloc in createdAllocations)
                {
                    var allocKey = $"alloc:{alloc.Id}";
                    if (!await ledgerRepository.ExistsByIdempotencyKeyAsync(
                            payment.OrganizationId, "PaymentAllocation", alloc.Id, allocKey, cancellationToken))
                    {
                        var jeNumber = await journalEntryNumberGenerator.GenerateNextAsync(payment.OrganizationId, utcNow, cancellationToken);
                        var je = LedgerPostingService.CreateAllocationEntry(payment, alloc, jeNumber, allocKey, utcNow);
                        await ledgerRepository.AddAsync(je, cancellationToken);
                    }
                }
            }

            await PaymentWorkflowHelper.RecordAuditAsync(
                auditRepository,
                request.OrganizationId,
                "Payment.Allocated",
                "Payment",
                payment.Id,
                new
                {
                    AllocationsCreated = createdAllocations.Count,
                    payment.AllocatedAmount,
                    payment.UnappliedAmount
                },
                utcNow,
                request.ActorUserId,
                cancellationToken: cancellationToken);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return Result<PaymentResponse>.Failure(new Error("Payment.AllocationError", ex.Message, Type: ErrorType.Validation));
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result<PaymentResponse>.Success(PaymentResponse.FromEntity(payment));
    }
}
