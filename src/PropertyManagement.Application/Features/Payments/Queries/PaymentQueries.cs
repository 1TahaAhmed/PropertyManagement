using MediatR;
using PropertyManagement.Application.Abstractions.Payments;
using PropertyManagement.Application.Common.Enums;
using PropertyManagement.Application.Common.Models;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Application.Features.Payments.Responses;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Application.Features.Payments.Queries;

public sealed record GetPaymentsQuery(
    Guid OrganizationId,
    Guid? ResidentId = null,
    Guid? LeaseId = null,
    PaymentStatus? Status = null,
    PaymentMethod? PaymentMethod = null,
    PaymentProviderType? Provider = null,
    bool OnlyUnapplied = false,
    int PageNumber = 1,
    int PageSize = 10) : IRequest<Result<PagedList<PaymentResponse>>>;

public sealed class GetPaymentsQueryHandler(IPaymentRepository paymentRepository)
    : IRequestHandler<GetPaymentsQuery, Result<PagedList<PaymentResponse>>>
{
    public async Task<Result<PagedList<PaymentResponse>>> Handle(GetPaymentsQuery request, CancellationToken cancellationToken)
    {
        var (items, totalCount) = await paymentRepository.GetPagedAsync(
            new PaymentFilter(
                request.OrganizationId,
                request.ResidentId,
                request.LeaseId,
                request.Status,
                request.PaymentMethod,
                request.Provider,
                OnlyUnapplied: request.OnlyUnapplied,
                PageNumber: request.PageNumber,
                PageSize: request.PageSize,
                IncludeDetails: false,
                AsNoTracking: true),
            cancellationToken);

        var responses = items.Select(PaymentResponse.FromEntity).ToList();
        return Result<PagedList<PaymentResponse>>.Success(
            new PagedList<PaymentResponse>(responses, request.PageNumber, request.PageSize, totalCount));
    }
}

public sealed record GetPaymentByIdQuery(Guid OrganizationId, Guid PaymentId) : IRequest<Result<PaymentResponse>>;

public sealed class GetPaymentByIdQueryHandler(IPaymentRepository paymentRepository)
    : IRequestHandler<GetPaymentByIdQuery, Result<PaymentResponse>>
{
    public async Task<Result<PaymentResponse>> Handle(GetPaymentByIdQuery request, CancellationToken cancellationToken)
    {
        var payment = await paymentRepository.GetByIdAsync(request.PaymentId, request.OrganizationId, cancellationToken);
        if (payment is null || payment.OrganizationId != request.OrganizationId)
        {
            return Result<PaymentResponse>.Failure(new Error("Payment.NotFound", "The specified payment was not found.", Type: ErrorType.NotFound));
        }

        return Result<PaymentResponse>.Success(PaymentResponse.FromEntity(payment));
    }
}

public sealed record GetPaymentRefundsQuery(Guid OrganizationId, Guid PaymentId)
    : IRequest<Result<IReadOnlyList<RefundResponse>>>;

public sealed class GetPaymentRefundsQueryHandler(IPaymentRepository paymentRepository)
    : IRequestHandler<GetPaymentRefundsQuery, Result<IReadOnlyList<RefundResponse>>>
{
    public async Task<Result<IReadOnlyList<RefundResponse>>> Handle(GetPaymentRefundsQuery request, CancellationToken cancellationToken)
    {
        var payment = await paymentRepository.GetByIdAsync(request.PaymentId, request.OrganizationId, cancellationToken);
        if (payment is null || payment.OrganizationId != request.OrganizationId)
        {
            return Result<IReadOnlyList<RefundResponse>>.Failure(new Error("Payment.NotFound", "The specified payment was not found.", Type: ErrorType.NotFound));
        }

        return Result<IReadOnlyList<RefundResponse>>.Success(
            payment.Refunds.Select(RefundResponse.FromEntity).ToList());
    }
}

public sealed record GetReconciliationRecordsQuery(
    Guid OrganizationId,
    Guid? BatchId = null,
    PaymentProviderType? Provider = null,
    ReconciliationStatus? Status = null,
    int PageNumber = 1,
    int PageSize = 20) : IRequest<Result<PagedList<ReconciliationRecordResponse>>>;

public sealed class GetReconciliationRecordsQueryHandler(IPaymentReconciliationRepository reconciliationRepository)
    : IRequestHandler<GetReconciliationRecordsQuery, Result<PagedList<ReconciliationRecordResponse>>>
{
    public async Task<Result<PagedList<ReconciliationRecordResponse>>> Handle(GetReconciliationRecordsQuery request, CancellationToken cancellationToken)
    {
        var (items, totalCount) = await reconciliationRepository.GetPagedAsync(
            new ReconciliationFilter(
                request.OrganizationId,
                request.BatchId,
                request.Provider,
                request.Status,
                request.PageNumber,
                request.PageSize),
            cancellationToken);

        return Result<PagedList<ReconciliationRecordResponse>>.Success(
            new PagedList<ReconciliationRecordResponse>(
                items.Select(ReconciliationRecordResponse.FromEntity).ToList(),
                request.PageNumber,
                request.PageSize,
                totalCount));
    }
}
