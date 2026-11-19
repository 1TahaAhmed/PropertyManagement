using System.Data;
using FluentValidation;
using MediatR;
using PropertyManagement.Application.Abstractions;
using PropertyManagement.Application.Abstractions.Audit;
using PropertyManagement.Application.Abstractions.Payments;
using PropertyManagement.Application.Common.Enums;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Application.Features.Payments.Common;
using PropertyManagement.Application.Features.Payments.Responses;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Application.Features.Payments.Lifecycle;

public sealed record CancelPaymentCommand(
    Guid OrganizationId,
    Guid ActorUserId,
    Guid PaymentId,
    string Reason,
    string? RowVersion = null) : IRequest<Result<PaymentResponse>>;

public sealed class CancelPaymentCommandValidator : AbstractValidator<CancelPaymentCommand>
{
    public CancelPaymentCommandValidator()
    {
        RuleFor(x => x.OrganizationId).NotEmpty();
        RuleFor(x => x.ActorUserId).NotEmpty();
        RuleFor(x => x.PaymentId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}

public sealed class CancelPaymentCommandHandler(
    IPaymentRepository paymentRepository,
    IFinancialAuditLogRepository auditRepository,
    IUnitOfWork unitOfWork,
    IClock clock) : IRequestHandler<CancelPaymentCommand, Result<PaymentResponse>>
{
    public async Task<Result<PaymentResponse>> Handle(CancelPaymentCommand request, CancellationToken cancellationToken)
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
            return Result<PaymentResponse>.Failure(new Error("Payment.InvalidState", $"Cannot cancel payment in '{payment.Status}' status.", Type: ErrorType.Conflict));
        }

        var utcNow = clock.UtcNow;
        payment.Cancel(request.Reason, utcNow);

        await PaymentWorkflowHelper.RecordAuditAsync(
            auditRepository,
            request.OrganizationId,
            "Payment.Cancelled",
            "Payment",
            payment.Id,
            new { request.Reason },
            utcNow,
            request.ActorUserId,
            cancellationToken: cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result<PaymentResponse>.Success(PaymentResponse.FromEntity(payment));
    }
}
