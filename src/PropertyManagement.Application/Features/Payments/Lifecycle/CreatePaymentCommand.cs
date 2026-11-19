using System.Data;
using FluentValidation;
using MediatR;
using PropertyManagement.Application.Abstractions;
using PropertyManagement.Application.Abstractions.Audit;
using PropertyManagement.Application.Abstractions.Billing;
using PropertyManagement.Application.Abstractions.Ledger;
using PropertyManagement.Application.Abstractions.Payments;
using PropertyManagement.Application.Abstractions.Repositories;
using PropertyManagement.Application.Abstractions.Residents;
using PropertyManagement.Application.Common.Enums;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Application.Features.Payments.Common;
using PropertyManagement.Application.Features.Payments.Responses;
using PropertyManagement.Domain.Common;
using PropertyManagement.Domain.Entities.Payments;
using PropertyManagement.Domain.Entities.Payments.Services;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Application.Features.Payments.Lifecycle;

public sealed record CreatePaymentCommand(
    Guid OrganizationId,
    Guid ActorUserId,
    Guid ResidentId,
    decimal Amount,
    string Currency,
    PaymentMethod PaymentMethod,
    PaymentProviderType Provider,
    Guid? LeaseId = null,
    Guid? PaymentIntentId = null,
    OverpaymentPolicy OverpaymentPolicy = OverpaymentPolicy.KeepUnappliedCredit,
    AllocationStrategy AllocationStrategy = AllocationStrategy.Explicit,
    IReadOnlyList<RequestedInvoiceAllocation>? Allocations = null,
    bool ConfirmImmediately = true,
    string? IdempotencyKey = null,
    string? Reference = null,
    string? MetadataJson = null,
    DateTime? ReceivedAtUtc = null) : IRequest<Result<PaymentResponse>>;

public sealed class CreatePaymentCommandValidator : AbstractValidator<CreatePaymentCommand>
{
    public CreatePaymentCommandValidator()
    {
        RuleFor(x => x.OrganizationId).NotEmpty();
        RuleFor(x => x.ActorUserId).NotEmpty();
        RuleFor(x => x.ResidentId).NotEmpty();
        RuleFor(x => x.Amount).GreaterThan(0m);
        RuleFor(x => x.Currency)
            .NotEmpty()
            .Must(CurrencyCode.IsValid)
            .WithMessage("Currency must be a valid 3-letter ISO code.");
        RuleFor(x => x.PaymentMethod).IsInEnum();
        RuleFor(x => x.Provider).IsInEnum();
        RuleFor(x => x.OverpaymentPolicy).IsInEnum();
        RuleFor(x => x.AllocationStrategy).IsInEnum();
        RuleFor(x => x.IdempotencyKey).MaximumLength(150);
        RuleFor(x => x.Reference).MaximumLength(200);
        RuleForEach(x => x.Allocations).ChildRules(item =>
        {
            item.RuleFor(a => a.InvoiceId).NotEmpty();
            item.RuleFor(a => a.Amount).GreaterThan(0m);
        });
    }
}

public sealed class CreatePaymentCommandHandler(
    IPaymentRepository paymentRepository,
    IPaymentIntentRepository intentRepository,
    IPaymentNumberGenerator paymentNumberGenerator,
    IResidentRepository residentRepository,
    ILeaseRepository leaseRepository,
    IInvoiceRepository invoiceRepository,
    ILedgerRepository ledgerRepository,
    IJournalEntryNumberGenerator journalEntryNumberGenerator,
    IFinancialAuditLogRepository auditRepository,
    IPaymentProviderFactory providerFactory,
    IUnitOfWork unitOfWork,
    IClock clock) : IRequestHandler<CreatePaymentCommand, Result<PaymentResponse>>
{
    public async Task<Result<PaymentResponse>> Handle(CreatePaymentCommand request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            var existing = await paymentRepository.GetByIdempotencyKeyAsync(
                request.IdempotencyKey, request.OrganizationId, cancellationToken);
            if (existing is not null)
            {
                return Result<PaymentResponse>.Success(PaymentResponse.FromEntity(existing));
            }
        }

        if (request.PaymentMethod == PaymentMethod.Cash && request.Provider != PaymentProviderType.Manual)
        {
            return Result<PaymentResponse>.Failure(new Error("Payment.InvalidProviderForCash", "Cash payments must use the Manual payment provider.", Type: ErrorType.Validation));
        }

        var resident = await residentRepository.GetByIdAsync(request.ResidentId, request.OrganizationId, cancellationToken);
        if (resident is null || resident.OrganizationId != request.OrganizationId)
        {
            return Result<PaymentResponse>.Failure(new Error("Payment.ResidentNotFound", "The specified resident was not found in this organization.", Type: ErrorType.NotFound));
        }

        if (request.LeaseId.HasValue)
        {
            var lease = await leaseRepository.GetByIdAsync(request.LeaseId.Value, request.OrganizationId, cancellationToken);
            if (lease is null || lease.OrganizationId != request.OrganizationId)
            {
                return Result<PaymentResponse>.Failure(new Error("Payment.LeaseNotFound", "The specified lease was not found in this organization.", Type: ErrorType.NotFound));
            }
        }

        PaymentIntent? linkedIntent = null;
        if (request.PaymentIntentId.HasValue)
        {
            linkedIntent = await intentRepository.GetByIdAsync(request.PaymentIntentId.Value, request.OrganizationId, cancellationToken);
            if (linkedIntent is null || linkedIntent.OrganizationId != request.OrganizationId)
            {
                return Result<PaymentResponse>.Failure(new Error("Payment.IntentNotFound", "The specified payment intent was not found.", Type: ErrorType.NotFound));
            }

            if (linkedIntent.ResidentId != request.ResidentId)
            {
                return Result<PaymentResponse>.Failure(new Error("Payment.IntentResidentMismatch", "Payment intent belongs to a different resident.", Type: ErrorType.Validation));
            }
        }

        var utcNow = clock.UtcNow;
        var payment = Payment.Create(
            request.OrganizationId,
            request.ResidentId,
            request.Amount,
            request.Currency,
            request.PaymentMethod,
            request.Provider,
            utcNow,
            request.LeaseId,
            request.PaymentIntentId,
            request.OverpaymentPolicy,
            request.IdempotencyKey,
            request.Reference,
            request.MetadataJson,
            request.ActorUserId,
            request.ReceivedAtUtc);

        await using var transaction = await unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        await paymentRepository.AcquireResidentLockAsync(request.ResidentId, request.OrganizationId, cancellationToken);

        // Re-check idempotency inside lock against concurrent duplicate submissions
        if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            var existingUnderLock = await paymentRepository.GetByIdempotencyKeyAsync(
                request.IdempotencyKey, request.OrganizationId, cancellationToken);
            if (existingUnderLock is not null)
            {
                return Result<PaymentResponse>.Success(PaymentResponse.FromEntity(existingUnderLock));
            }
        }

        payment.AssignPaymentNumber(await paymentNumberGenerator.GenerateNextAsync(request.OrganizationId, utcNow, cancellationToken));

        var provider = providerFactory.Resolve(request.Provider);
        if (request.Provider == PaymentProviderType.Manual)
        {
            if (request.PaymentMethod == PaymentMethod.Cash || request.ConfirmImmediately)
            {
                payment.MarkSucceeded(
                    utcNow,
                    providerStatus: "manual_confirmed",
                    receivedAtUtc: request.ReceivedAtUtc ?? utcNow,
                    confirmedByUserId: request.ActorUserId);
            }
        }
        else
        {
            var providerResult = await provider.CreateOrCapturePaymentAsync(
                new ProviderPaymentRequest(
                    request.OrganizationId,
                    payment.Id,
                    request.ResidentId,
                    payment.Amount,
                    payment.Currency,
                    request.PaymentMethod,
                    request.IdempotencyKey ?? payment.Id.ToString("N"),
                    linkedIntent?.ProviderIntentId,
                    request.Reference),
                cancellationToken);

            payment.AttachExternalIdentifiers(
                providerResult.ProviderPaymentId,
                providerResult.ProviderCustomerId,
                providerResult.ProviderTransactionId,
                providerResult.ProviderStatus,
                utcNow);

            if (!providerResult.CallSucceeded)
            {
                payment.MarkFailed(providerResult.FailureReason ?? "External provider rejected payment.", utcNow, providerResult.ProviderStatus);
                linkedIntent?.MarkFailed(providerResult.FailureReason ?? "External provider rejected payment.", utcNow);
            }
            else if (providerResult.IsPaid)
            {
                payment.MarkSucceeded(
                    utcNow,
                    providerTransactionId: providerResult.ProviderTransactionId,
                    providerStatus: providerResult.ProviderStatus,
                    providerFee: providerResult.ProviderFee,
                    receivedAtUtc: utcNow);
                linkedIntent?.MarkSucceeded(utcNow);
            }
            else if (providerResult.IsProcessing)
            {
                payment.MarkProcessing(providerResult.ProviderPaymentId, providerResult.ProviderStatus, utcNow);
                if (linkedIntent is not null && linkedIntent.Status is PaymentIntentStatus.Pending or PaymentIntentStatus.RequiresAction)
                {
                    linkedIntent.MarkProcessing(utcNow);
                }
            }
        }

        if (payment.Status == PaymentStatus.Succeeded)
        {
            try
            {
                var hasAllocationsOrAuto =
                    (request.Allocations is { Count: > 0 }) ||
                    request.AllocationStrategy != AllocationStrategy.Explicit ||
                    request.OverpaymentPolicy == OverpaymentPolicy.RejectOverpayment;

                if (hasAllocationsOrAuto)
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
                return Result<PaymentResponse>.Failure(new Error("Payment.AllocationError", ex.Message, Type: ErrorType.Validation));
            }
        }

        await paymentRepository.AddAsync(payment, cancellationToken);
        await PaymentWorkflowHelper.RecordAuditAsync(
            auditRepository,
            request.OrganizationId,
            $"Payment.{payment.Status}",
            "Payment",
            payment.Id,
            new
            {
                payment.PaymentNumber,
                payment.Amount,
                payment.Currency,
                Method = payment.PaymentMethod.ToString(),
                Provider = payment.Provider.ToString(),
                payment.AllocatedAmount,
                payment.UnappliedAmount
            },
            utcNow,
            request.ActorUserId,
            request.IdempotencyKey,
            cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result<PaymentResponse>.Success(PaymentResponse.FromEntity(payment));
    }
}
