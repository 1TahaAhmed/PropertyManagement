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
using PropertyManagement.Domain.Entities.Payments.Services;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Application.Features.Payments.Reconciliation;

public sealed record RecordProviderSettlementCommand(
    Guid OrganizationId,
    Guid ActorUserId,
    Guid PaymentId,
    decimal ProviderFee,
    DateTime SettledAtUtc) : IRequest<Result<PaymentResponse>>;

public sealed class RecordProviderSettlementCommandValidator : AbstractValidator<RecordProviderSettlementCommand>
{
    public RecordProviderSettlementCommandValidator()
    {
        RuleFor(x => x.OrganizationId).NotEmpty();
        RuleFor(x => x.ActorUserId).NotEmpty();
        RuleFor(x => x.PaymentId).NotEmpty();
        RuleFor(x => x.ProviderFee).GreaterThanOrEqualTo(0m);
    }
}

public sealed class RecordProviderSettlementCommandHandler(
    IPaymentRepository paymentRepository,
    ILedgerRepository ledgerRepository,
    IJournalEntryNumberGenerator journalEntryNumberGenerator,
    IFinancialAuditLogRepository auditRepository,
    IUnitOfWork unitOfWork,
    IClock clock) : IRequestHandler<RecordProviderSettlementCommand, Result<PaymentResponse>>
{
    public async Task<Result<PaymentResponse>> Handle(RecordProviderSettlementCommand request, CancellationToken cancellationToken)
    {
        await using var transaction = await unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        await paymentRepository.AcquirePaymentLockAsync(request.PaymentId, request.OrganizationId, cancellationToken);

        var payment = await paymentRepository.GetByIdAsync(request.PaymentId, request.OrganizationId, cancellationToken);
        if (payment is null || payment.OrganizationId != request.OrganizationId)
        {
            return Result<PaymentResponse>.Failure(new Error("Payment.NotFound", "The specified payment was not found.", Type: ErrorType.NotFound));
        }

        if (payment.Provider == PaymentProviderType.Manual)
        {
            return Result<PaymentResponse>.Failure(new Error("Payment.ManualNoSettlement", "Manual payments do not settle through a provider clearing account.", Type: ErrorType.Validation));
        }

        var utcNow = clock.UtcNow;
        try
        {
            payment.RecordSettlement(request.ProviderFee, request.SettledAtUtc, utcNow);

            var settleKey = $"settle:{payment.Id}";
            if (!await ledgerRepository.ExistsByIdempotencyKeyAsync(
                    payment.OrganizationId, "PaymentSettled", payment.Id, settleKey, cancellationToken))
            {
                var jeNumber = await journalEntryNumberGenerator.GenerateNextAsync(payment.OrganizationId, utcNow, cancellationToken);
                var je = LedgerPostingService.CreateProviderSettlementEntry(payment, jeNumber, settleKey, utcNow);
                await ledgerRepository.AddAsync(je, cancellationToken);
            }

            await PaymentWorkflowHelper.RecordAuditAsync(
                auditRepository,
                request.OrganizationId,
                "Payment.Settled",
                "Payment",
                payment.Id,
                new { payment.Amount, payment.ProviderFee, payment.NetSettlementAmount },
                utcNow,
                request.ActorUserId,
                cancellationToken: cancellationToken);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return Result<PaymentResponse>.Failure(new Error("Payment.SettlementError", ex.Message, Type: ErrorType.Validation));
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result<PaymentResponse>.Success(PaymentResponse.FromEntity(payment));
    }
}

public sealed record ReconcilePaymentsCommand(
    Guid OrganizationId,
    Guid ActorUserId,
    PaymentProviderType Provider,
    DateTime FromUtc,
    DateTime ToUtc,
    IReadOnlyList<ProviderTransactionSnapshot>? ProviderTransactionsOverride = null) : IRequest<Result<ReconciliationBatchResponse>>;

public sealed class ReconcilePaymentsCommandValidator : AbstractValidator<ReconcilePaymentsCommand>
{
    public ReconcilePaymentsCommandValidator()
    {
        RuleFor(x => x.OrganizationId).NotEmpty();
        RuleFor(x => x.ActorUserId).NotEmpty();
        RuleFor(x => x.Provider).IsInEnum();
        RuleFor(x => x.ToUtc).GreaterThanOrEqualTo(x => x.FromUtc);
    }
}

public sealed class ReconcilePaymentsCommandHandler(
    IPaymentRepository paymentRepository,
    IPaymentReconciliationRepository reconciliationRepository,
    IPaymentProviderFactory providerFactory,
    IFinancialAuditLogRepository auditRepository,
    IUnitOfWork unitOfWork,
    IClock clock) : IRequestHandler<ReconcilePaymentsCommand, Result<ReconciliationBatchResponse>>
{
    public async Task<Result<ReconciliationBatchResponse>> Handle(ReconcilePaymentsCommand request, CancellationToken cancellationToken)
    {
        var internalPayments = await paymentRepository.GetByProviderAndWindowAsync(
            request.OrganizationId, request.Provider, request.FromUtc, request.ToUtc, cancellationToken);

        IReadOnlyCollection<ProviderTransactionSnapshot> providerTransactions;
        if (request.ProviderTransactionsOverride is not null)
        {
            providerTransactions = request.ProviderTransactionsOverride;
        }
        else
        {
            var provider = providerFactory.Resolve(request.Provider);
            providerTransactions = await provider.FetchSettlementReportAsync(request.FromUtc, request.ToUtc, cancellationToken);
        }

        var utcNow = clock.UtcNow;
        var batchId = Guid.NewGuid();
        var records = PaymentReconciliationEngine.Reconcile(
            request.OrganizationId,
            batchId,
            request.Provider,
            internalPayments,
            providerTransactions,
            utcNow);

        await reconciliationRepository.AddRangeAsync(records, cancellationToken);

        var matchedCount = records.Count(r => r.Status == ReconciliationStatus.Matched);
        var exceptionCount = records.Count - matchedCount;

        await PaymentWorkflowHelper.RecordAuditAsync(
            auditRepository,
            request.OrganizationId,
            "Reconciliation.Executed",
            "ReconciliationBatch",
            batchId,
            new { Provider = request.Provider.ToString(), TotalChecked = records.Count, matchedCount, exceptionCount },
            utcNow,
            request.ActorUserId,
            cancellationToken: cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<ReconciliationBatchResponse>.Success(new ReconciliationBatchResponse(
            batchId,
            request.Provider,
            records.Count,
            matchedCount,
            exceptionCount,
            records.Select(ReconciliationRecordResponse.FromEntity).ToList()));
    }
}

public sealed record ResolveReconciliationRecordCommand(
    Guid OrganizationId,
    Guid ActorUserId,
    Guid RecordId,
    string ResolutionNotes) : IRequest<Result<ReconciliationRecordResponse>>;

public sealed class ResolveReconciliationRecordCommandValidator : AbstractValidator<ResolveReconciliationRecordCommand>
{
    public ResolveReconciliationRecordCommandValidator()
    {
        RuleFor(x => x.OrganizationId).NotEmpty();
        RuleFor(x => x.ActorUserId).NotEmpty();
        RuleFor(x => x.RecordId).NotEmpty();
        RuleFor(x => x.ResolutionNotes).NotEmpty().MaximumLength(1000);
    }
}

public sealed class ResolveReconciliationRecordCommandHandler(
    IPaymentReconciliationRepository reconciliationRepository,
    IFinancialAuditLogRepository auditRepository,
    IUnitOfWork unitOfWork,
    IClock clock) : IRequestHandler<ResolveReconciliationRecordCommand, Result<ReconciliationRecordResponse>>
{
    public async Task<Result<ReconciliationRecordResponse>> Handle(ResolveReconciliationRecordCommand request, CancellationToken cancellationToken)
    {
        var record = await reconciliationRepository.GetByIdAsync(request.RecordId, request.OrganizationId, cancellationToken);
        if (record is null || record.OrganizationId != request.OrganizationId)
        {
            return Result<ReconciliationRecordResponse>.Failure(new Error("Reconciliation.NotFound", "The specified reconciliation record was not found.", Type: ErrorType.NotFound));
        }

        var utcNow = clock.UtcNow;
        try
        {
            record.Resolve(request.ActorUserId, request.ResolutionNotes, utcNow);
        }
        catch (InvalidOperationException ex)
        {
            return Result<ReconciliationRecordResponse>.Failure(new Error("Reconciliation.InvalidState", ex.Message, Type: ErrorType.Conflict));
        }

        await PaymentWorkflowHelper.RecordAuditAsync(
            auditRepository,
            request.OrganizationId,
            "Reconciliation.Resolved",
            "PaymentReconciliationRecord",
            record.Id,
            new { request.ResolutionNotes },
            utcNow,
            request.ActorUserId,
            cancellationToken: cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<ReconciliationRecordResponse>.Success(ReconciliationRecordResponse.FromEntity(record));
    }
}
