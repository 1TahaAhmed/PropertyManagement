using System.Data;
using MediatR;
using PropertyManagement.Application.Abstractions;
using PropertyManagement.Application.Abstractions.Billing;
using PropertyManagement.Application.Common.Enums;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Application.Features.Billing.Responses;

namespace PropertyManagement.Application.Features.Billing.Invoices;

public sealed class ApplyInvoiceAdjustmentCommandHandler(
    IInvoiceRepository invoiceRepository,
    IUnitOfWork unitOfWork,
    IClock clock) : IRequestHandler<ApplyInvoiceAdjustmentCommand, Result<InvoiceResponse>>
{
    public async Task<Result<InvoiceResponse>> Handle(ApplyInvoiceAdjustmentCommand request, CancellationToken cancellationToken)
    {
        await using var transaction = await unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        await invoiceRepository.AcquireInvoiceLockAsync(request.InvoiceId, request.OrganizationId, cancellationToken);

        var invoice = await invoiceRepository.GetByIdAsync(request.InvoiceId, request.OrganizationId, cancellationToken);
        if (invoice is null || invoice.OrganizationId != request.OrganizationId)
        {
            return Result<InvoiceResponse>.Failure(new Error(
                Code: "Invoice.NotFound",
                Message: "The specified invoice was not found.",
                Type: ErrorType.NotFound));
        }

        if (!string.IsNullOrWhiteSpace(request.RowVersion))
        {
            try
            {
                invoiceRepository.SetRowVersion(invoice, Convert.FromBase64String(request.RowVersion));
            }
            catch (FormatException)
            {
                return Result<InvoiceResponse>.Failure(new Error(
                    Code: "Invoice.InvalidRowVersion",
                    Message: "The provided RowVersion concurrency token is invalid.",
                    Type: ErrorType.Validation));
            }
        }

        try
        {
            invoice.ApplyAdjustment(
                request.Type,
                request.ChargeType,
                request.Amount,
                request.Description,
                request.Reason,
                request.ActorUserId,
                clock.UtcNow);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return Result<InvoiceResponse>.Failure(new Error(
                Code: "Invoice.AdjustmentError",
                Message: ex.Message,
                Type: ErrorType.Validation));
        }

        invoiceRepository.Update(invoice);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result<InvoiceResponse>.Success(InvoiceResponse.FromEntity(invoice));
    }
}
