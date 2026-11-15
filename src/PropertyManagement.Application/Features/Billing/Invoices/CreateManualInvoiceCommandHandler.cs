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
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Application.Features.Billing.Invoices;

public sealed class CreateManualInvoiceCommandHandler(
    ILeaseRepository leaseRepository,
    IChargeRepository chargeRepository,
    IInvoiceRepository invoiceRepository,
    IInvoiceNumberGenerator invoiceNumberGenerator,
    IUnitOfWork unitOfWork,
    IClock clock) : IRequestHandler<CreateManualInvoiceCommand, Result<InvoiceResponse>>
{
    public async Task<Result<InvoiceResponse>> Handle(CreateManualInvoiceCommand request, CancellationToken cancellationToken)
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

        if (lease.Status == LeaseStatus.Cancelled)
        {
            return Result<InvoiceResponse>.Failure(new Error(
                Code: "Lease.Cancelled",
                Message: "Cannot create an invoice for a cancelled lease.",
                Type: ErrorType.Validation));
        }

        var residentResult = BillingLeaseHelper.ResolveBillableResidentId(lease);
        if (residentResult.IsFailure)
        {
            return Result<InvoiceResponse>.Failure(residentResult.Errors.ToArray());
        }

        var trimmedKey = string.IsNullOrWhiteSpace(request.IdempotencyKey) ? null : request.IdempotencyKey.Trim();
        if (!string.IsNullOrWhiteSpace(trimmedKey))
        {
            var exists = await invoiceRepository.ExistsActiveByIdempotencyKeyAsync(trimmedKey, request.OrganizationId, cancellationToken);
            if (exists)
            {
                return Result<InvoiceResponse>.Failure(new Error(
                    Code: "Billing.DuplicateInvoice",
                    Message: $"An active invoice already exists for idempotency key '{trimmedKey}'.",
                    Type: ErrorType.Conflict));
            }
        }

        var chargesResult = await BillingLeaseHelper.ResolvePendingChargesAsync(
            chargeRepository,
            lease,
            request.OrganizationId,
            includePendingCharges: false,
            request.ChargeIds,
            request.PeriodEnd,
            cancellationToken);

        if (chargesResult.IsFailure)
        {
            return Result<InvoiceResponse>.Failure(chargesResult.Errors.ToArray());
        }

        var utcNow = clock.UtcNow;
        var issueDate = request.IssueDate ?? DateOnly.FromDateTime(utcNow);

        Invoice invoice;
        try
        {
            invoice = Invoice.CreateDraft(
                organizationId: request.OrganizationId,
                leaseId: lease.Id,
                propertyId: lease.PropertyId,
                unitId: lease.UnitId,
                residentId: residentResult.Value,
                billingPeriod: new BillingPeriod(request.PeriodStart, request.PeriodEnd),
                issueDate: issueDate,
                dueDate: request.DueDate,
                currency: lease.Currency,
                utcNow: utcNow,
                notes: request.Notes,
                idempotencyKey: trimmedKey);

            if (request.Lines is { Count: > 0 })
            {
                foreach (var line in request.Lines)
                {
                    invoice.AddLine(
                        type: line.Type,
                        description: line.Description,
                        quantity: line.Quantity,
                        unitPrice: line.UnitPrice,
                        utcNow: utcNow,
                        periodStart: line.PeriodStart,
                        periodEnd: line.PeriodEnd);
                }
            }

            foreach (var charge in chargesResult.Value)
            {
                invoice.AddLine(
                    type: charge.Type,
                    description: charge.Description,
                    quantity: charge.Quantity,
                    unitPrice: charge.UnitPrice,
                    utcNow: utcNow,
                    sourceChargeId: charge.Id,
                    periodStart: charge.PeriodStart,
                    periodEnd: charge.PeriodEnd);

                charge.MarkBilled(invoice.Id, utcNow);
                chargeRepository.Update(charge);
            }

            if (invoice.Total < 0m)
            {
                return Result<InvoiceResponse>.Failure(new Error(
                    Code: "Billing.NegativeTotal",
                    Message: $"Calculated invoice total ({invoice.Total:0.00}) cannot be negative.",
                    Type: ErrorType.Validation));
            }

            if (request.AutoIssue)
            {
                var number = await invoiceNumberGenerator.GenerateNextAsync(request.OrganizationId, issueDate.Year, cancellationToken);
                invoice.Issue(number, issueDate, utcNow);
            }
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return Result<InvoiceResponse>.Failure(new Error(
                Code: "Invoice.ValidationError",
                Message: ex.Message,
                Type: ErrorType.Validation));
        }

        await invoiceRepository.AddAsync(invoice, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result<InvoiceResponse>.Success(InvoiceResponse.FromEntity(invoice));
    }
}
