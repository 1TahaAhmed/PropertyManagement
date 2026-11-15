using MediatR;
using PropertyManagement.Application.Abstractions;
using PropertyManagement.Application.Abstractions.Billing;
using PropertyManagement.Application.Abstractions.Repositories;
using PropertyManagement.Application.Common.Enums;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Application.Features.Billing.Common;
using PropertyManagement.Application.Features.Billing.Responses;
using PropertyManagement.Domain.Entities.Billing;
using PropertyManagement.Domain.Entities.Billing.Services;

namespace PropertyManagement.Application.Features.Billing.Preview;

public sealed class PreviewBillingQueryHandler(
    ILeaseRepository leaseRepository,
    IChargeRepository chargeRepository,
    IInvoiceRepository invoiceRepository,
    IBillingCalculationEngine calculationEngine,
    IClock clock) : IRequestHandler<PreviewBillingQuery, Result<BillingPreviewResponse>>
{
    public async Task<Result<BillingPreviewResponse>> Handle(PreviewBillingQuery request, CancellationToken cancellationToken)
    {
        var lease = await leaseRepository.GetByIdAsync(request.LeaseId, request.OrganizationId, cancellationToken);
        if (lease is null || lease.OrganizationId != request.OrganizationId)
        {
            return Result<BillingPreviewResponse>.Failure(new Error(
                Code: "Lease.NotFound",
                Message: "The specified lease was not found.",
                Type: ErrorType.NotFound));
        }

        if (!BillingLeaseHelper.IsBillableStatus(lease.Status))
        {
            return Result<BillingPreviewResponse>.Failure(new Error(
                Code: "Lease.NotBillable",
                Message: $"Cannot preview billing for lease in '{lease.Status}' status.",
                Type: ErrorType.Validation));
        }

        var residentResult = BillingLeaseHelper.ResolveBillableResidentId(lease);
        if (residentResult.IsFailure)
        {
            return Result<BillingPreviewResponse>.Failure(residentResult.Errors.ToArray());
        }

        var chargesResult = await BillingLeaseHelper.ResolvePendingChargesAsync(
            chargeRepository,
            lease,
            request.OrganizationId,
            request.IncludePendingCharges,
            request.ChargeIds,
            request.PeriodEnd,
            cancellationToken);

        if (chargesResult.IsFailure)
        {
            return Result<BillingPreviewResponse>.Failure(chargesResult.Errors.ToArray());
        }

        var effectiveIssueDate = request.IssueDate ?? DateOnly.FromDateTime(clock.UtcNow);
        var period = new BillingPeriod(request.PeriodStart, request.PeriodEnd);
        var (discounts, additionalCharges) = BillingLeaseHelper.MapChargesAndInputs(
            chargesResult.Value,
            request.Discounts,
            request.AdditionalCharges);

        BillingCalculationResult calculationResult;
        try
        {
            var calcRequest = BillingCalculationRequest.FromLease(
                lease: lease,
                billingPeriod: period,
                issueDate: effectiveIssueDate,
                dueDateOverride: request.DueDateOverride,
                discounts: discounts,
                additionalCharges: additionalCharges,
                includeRent: request.IncludeRent);

            calculationResult = calculationEngine.Calculate(calcRequest);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return Result<BillingPreviewResponse>.Failure(new Error(
                Code: "Billing.CalculationError",
                Message: ex.Message,
                Type: ErrorType.Validation));
        }

        var alreadyBilled = false;
        if (request.IncludeRent)
        {
            var idempotencyKey = BillingLeaseHelper.BuildRentIdempotencyKey(lease.Id, request.PeriodStart, request.PeriodEnd);
            alreadyBilled = await invoiceRepository.ExistsActiveByIdempotencyKeyAsync(
                idempotencyKey,
                request.OrganizationId,
                cancellationToken)
                || await invoiceRepository.ExistsActiveRentOverlapAsync(
                    lease.Id,
                    request.OrganizationId,
                    request.PeriodStart,
                    request.PeriodEnd,
                    cancellationToken);
        }

        var response = new BillingPreviewResponse(
            LeaseId: lease.Id,
            ResidentId: residentResult.Value,
            BillingPeriodStart: calculationResult.BillingPeriod.StartDate,
            BillingPeriodEnd: calculationResult.BillingPeriod.EndDate,
            EffectiveStart: calculationResult.EffectiveServicePeriod.StartDate,
            EffectiveEnd: calculationResult.EffectiveServicePeriod.EndDate,
            IsProrated: calculationResult.IsProrated,
            IssueDate: calculationResult.IssueDate,
            DueDate: calculationResult.DueDate,
            Currency: calculationResult.Currency,
            Subtotal: calculationResult.Subtotal,
            Total: calculationResult.Total,
            Lines: calculationResult.Lines.Select(l => new BillingPreviewLineResponse(
                l.Type,
                l.Description,
                l.Quantity,
                l.UnitPrice,
                l.Amount,
                l.PeriodStart,
                l.PeriodEnd,
                l.IsProrated,
                l.OccupiedDays,
                l.DaysInMonth,
                l.SourceChargeId)).ToList(),
            AlreadyBilledForPeriod: alreadyBilled);

        return Result<BillingPreviewResponse>.Success(response);
    }
}
