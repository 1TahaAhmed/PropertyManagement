using FluentValidation;
using MediatR;
using PropertyManagement.Application.Abstractions;
using PropertyManagement.Application.Abstractions.Audit;
using PropertyManagement.Application.Abstractions.Billing;
using PropertyManagement.Application.Abstractions.Payments;
using PropertyManagement.Application.Abstractions.Repositories;
using PropertyManagement.Application.Abstractions.Residents;
using PropertyManagement.Application.Common.Enums;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Application.Features.Payments.Common;
using PropertyManagement.Application.Features.Payments.Responses;
using PropertyManagement.Domain.Common;
using PropertyManagement.Domain.Entities.Payments;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Application.Features.Payments.Intents;

public sealed record CreatePaymentIntentCommand(
    Guid OrganizationId,
    Guid ActorUserId,
    Guid ResidentId,
    decimal Amount,
    string Currency,
    PaymentMethod PaymentMethod,
    PaymentProviderType Provider,
    Guid? LeaseId = null,
    Guid? TargetInvoiceId = null,
    int ExpiresInMinutes = 30,
    string? IdempotencyKey = null,
    string? Description = null) : IRequest<Result<PaymentIntentResponse>>;

public sealed class CreatePaymentIntentCommandValidator : AbstractValidator<CreatePaymentIntentCommand>
{
    public CreatePaymentIntentCommandValidator()
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
        RuleFor(x => x.ExpiresInMinutes).InclusiveBetween(5, 10080);
        RuleFor(x => x.IdempotencyKey).MaximumLength(150);
    }
}

public sealed class CreatePaymentIntentCommandHandler(
    IPaymentIntentRepository intentRepository,
    IResidentRepository residentRepository,
    ILeaseRepository leaseRepository,
    IInvoiceRepository invoiceRepository,
    IPaymentProviderFactory providerFactory,
    IFinancialAuditLogRepository auditRepository,
    IUnitOfWork unitOfWork,
    IClock clock) : IRequestHandler<CreatePaymentIntentCommand, Result<PaymentIntentResponse>>
{
    public async Task<Result<PaymentIntentResponse>> Handle(CreatePaymentIntentCommand request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            var existing = await intentRepository.GetByIdempotencyKeyAsync(
                request.IdempotencyKey, request.OrganizationId, cancellationToken);
            if (existing is not null)
            {
                return Result<PaymentIntentResponse>.Success(PaymentIntentResponse.FromEntity(existing));
            }
        }

        var resident = await residentRepository.GetByIdAsync(request.ResidentId, request.OrganizationId, cancellationToken);
        if (resident is null || resident.OrganizationId != request.OrganizationId)
        {
            return Result<PaymentIntentResponse>.Failure(new Error("PaymentIntent.ResidentNotFound", "The specified resident was not found in this organization.", Type: ErrorType.NotFound));
        }

        var normalizedCurrency = CurrencyCode.Normalize(request.Currency);

        if (request.LeaseId.HasValue)
        {
            var lease = await leaseRepository.GetByIdAsync(request.LeaseId.Value, request.OrganizationId, cancellationToken);
            if (lease is null || lease.OrganizationId != request.OrganizationId)
            {
                return Result<PaymentIntentResponse>.Failure(new Error("PaymentIntent.LeaseNotFound", "The specified lease was not found in this organization.", Type: ErrorType.NotFound));
            }
        }

        if (request.TargetInvoiceId.HasValue)
        {
            var invoice = await invoiceRepository.GetByIdAsync(request.TargetInvoiceId.Value, request.OrganizationId, cancellationToken);
            if (invoice is null || invoice.OrganizationId != request.OrganizationId)
            {
                return Result<PaymentIntentResponse>.Failure(new Error("PaymentIntent.InvoiceNotFound", "The specified target invoice was not found.", Type: ErrorType.NotFound));
            }

            if (!string.Equals(invoice.Currency, normalizedCurrency, StringComparison.Ordinal))
            {
                return Result<PaymentIntentResponse>.Failure(new Error("PaymentIntent.CurrencyMismatch", $"Intent currency '{normalizedCurrency}' does not match invoice currency '{invoice.Currency}'.", Type: ErrorType.Validation));
            }
        }

        var utcNow = clock.UtcNow;
        var intent = PaymentIntent.Create(
            request.OrganizationId,
            request.ResidentId,
            request.Amount,
            normalizedCurrency,
            request.PaymentMethod,
            request.Provider,
            utcNow.AddMinutes(request.ExpiresInMinutes),
            utcNow,
            request.LeaseId,
            request.TargetInvoiceId,
            request.IdempotencyKey);

        var provider = providerFactory.Resolve(request.Provider);
        var providerResult = await provider.CreateIntentAsync(
            new ProviderCreateIntentRequest(
                request.OrganizationId,
                intent.Id,
                request.ResidentId,
                intent.Amount,
                intent.Currency,
                request.PaymentMethod,
                request.IdempotencyKey ?? intent.Id.ToString("N"),
                request.Description),
            cancellationToken);

        if (!providerResult.Succeeded)
        {
            intent.MarkFailed(providerResult.ErrorMessage ?? "Provider failed to create payment intent.", utcNow);
            return Result<PaymentIntentResponse>.Failure(new Error("PaymentIntent.ProviderError", providerResult.ErrorMessage ?? "Payment provider rejected payment intent creation.", Type: ErrorType.Failure));
        }

        if (!string.IsNullOrWhiteSpace(providerResult.ProviderIntentId))
        {
            intent.AttachProviderSession(
                providerResult.ProviderIntentId,
                providerResult.ClientSecret,
                providerResult.ActionUrl,
                providerResult.RequiresAction,
                utcNow);
        }

        await intentRepository.AddAsync(intent, cancellationToken);
        await PaymentWorkflowHelper.RecordAuditAsync(
            auditRepository,
            request.OrganizationId,
            "PaymentIntent.Created",
            "PaymentIntent",
            intent.Id,
            new { intent.Amount, intent.Currency, intent.Provider, intent.PaymentMethod, intent.Status },
            utcNow,
            request.ActorUserId,
            request.IdempotencyKey,
            cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<PaymentIntentResponse>.Success(PaymentIntentResponse.FromEntity(intent));
    }
}
