using System.Data;
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

namespace PropertyManagement.Application.Features.Billing.Generate;

public sealed class GenerateLeaseInvoiceCommandHandler(
    ILeaseRepository leaseRepository,
    IChargeRepository chargeRepository,
    IInvoiceRepository invoiceRepository,
    IInvoiceNumberGenerator invoiceNumberGenerator,
    IBillingCalculationEngine calculationEngine,
    IUnitOfWork unitOfWork,
    IClock clock) : IRequestHandler<GenerateLeaseInvoiceCommand, Result<InvoiceResponse>>
{
    public async Task<Result<InvoiceResponse>> Handle(GenerateLeaseInvoiceCommand request, CancellationToken cancellationToken)
    {
        var initialLease = await leaseRepository.GetByIdAsync(request.LeaseId, request.OrganizationId, cancellationToken);
        if (initialLease is null || initialLease.OrganizationId != request.OrganizationId)
        {
            return Result<InvoiceResponse>.Failure(new Error(
                Code: "Lease.NotFound",
                Message: "The specified lease was not found.",
                Type: ErrorType.NotFound));
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        await leaseRepository.AcquireUnitLockAsync(initialLease.UnitId, request.OrganizationId, cancellationToken);

        var lease = await leaseRepository.GetByIdAsync(request.LeaseId, request.OrganizationId, cancellationToken);
        if (lease is null || lease.OrganizationId != request.OrganizationId)
        {
            return Result<InvoiceResponse>.Failure(new Error(
                Code: "Lease.NotFound",
                Message: "The specified lease was not found.",
                Type: ErrorType.NotFound));
        }

        if (!BillingLeaseHelper.IsBillableStatus(lease.Status))
        {
            return Result<InvoiceResponse>.Failure(new Error(
                Code: "Lease.NotBillable",
                Message: $"Cannot generate an invoice for lease in '{lease.Status}' status.",
                Type: ErrorType.Validation));
        }

        var residentResult = BillingLeaseHelper.ResolveBillableResidentId(lease);
        if (residentResult.IsFailure)
        {
            return Result<InvoiceResponse>.Failure(residentResult.Errors.ToArray());
        }

        string? effectiveIdempotencyKey = null;
        if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            effectiveIdempotencyKey = request.IdempotencyKey.Trim();
        }
        else if (request.IncludeRent)
        {
            effectiveIdempotencyKey = BillingLeaseHelper.BuildRentIdempotencyKey(lease.Id, request.PeriodStart, request.PeriodEnd);
        }

        if (!string.IsNullOrWhiteSpace(effectiveIdempotencyKey))
        {
            var exists = await invoiceRepository.ExistsActiveByIdempotencyKeyAsync(
                effectiveIdempotencyKey,
                request.OrganizationId,
                cancellationToken);

            if (exists)
            {
                return Result<InvoiceResponse>.Failure(new Error(
                    Code: "Billing.DuplicateInvoice",
                    Message: $"An active invoice already exists for idempotency key '{effectiveIdempotencyKey}'.",
                    Type: ErrorType.Conflict));
            }
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
            return Result<InvoiceResponse>.Failure(chargesResult.Errors.ToArray());
        }

        var utcNow = clock.UtcNow;
        var effectiveIssueDate = request.IssueDate ?? DateOnly.FromDateTime(utcNow);
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
            return Result<InvoiceResponse>.Failure(new Error(
                Code: "Billing.CalculationError",
                Message: ex.Message,
                Type: ErrorType.Validation));
        }

        var invoice = calculationResult.ToDraftInvoice(
            organizationId: request.OrganizationId,
            leaseId: lease.Id,
            propertyId: lease.PropertyId,
            unitId: lease.UnitId,
            residentId: residentResult.Value,
            utcNow: utcNow,
            notes: request.Notes,
            idempotencyKey: effectiveIdempotencyKey);

        foreach (var charge in chargesResult.Value)
        {
            charge.MarkBilled(invoice.Id, utcNow);
            chargeRepository.Update(charge);
        }

        if (request.AutoIssue)
        {
            var invoiceNumber = await invoiceNumberGenerator.GenerateNextAsync(
                request.OrganizationId,
                calculationResult.IssueDate.Year,
                cancellationToken);

            invoice.Issue(invoiceNumber, calculationResult.IssueDate, utcNow);
        }

        await invoiceRepository.AddAsync(invoice, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result<InvoiceResponse>.Success(InvoiceResponse.FromEntity(invoice));
    }
}
