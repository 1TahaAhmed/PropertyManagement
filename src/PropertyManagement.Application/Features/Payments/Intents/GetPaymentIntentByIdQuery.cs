using MediatR;
using PropertyManagement.Application.Abstractions.Payments;
using PropertyManagement.Application.Common.Enums;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Application.Features.Payments.Responses;

namespace PropertyManagement.Application.Features.Payments.Intents;

public sealed record GetPaymentIntentByIdQuery(Guid OrganizationId, Guid PaymentIntentId)
    : IRequest<Result<PaymentIntentResponse>>;

public sealed class GetPaymentIntentByIdQueryHandler(IPaymentIntentRepository intentRepository)
    : IRequestHandler<GetPaymentIntentByIdQuery, Result<PaymentIntentResponse>>
{
    public async Task<Result<PaymentIntentResponse>> Handle(
        GetPaymentIntentByIdQuery request,
        CancellationToken cancellationToken)
    {
        var intent = await intentRepository.GetByIdAsync(
            request.PaymentIntentId,
            request.OrganizationId,
            cancellationToken);

        if (intent is null || intent.OrganizationId != request.OrganizationId)
        {
            return Result<PaymentIntentResponse>.Failure(new Error(
                "PaymentIntent.NotFound",
                "The specified payment intent was not found.",
                Type: ErrorType.NotFound));
        }

        return Result<PaymentIntentResponse>.Success(PaymentIntentResponse.FromEntity(intent));
    }
}
