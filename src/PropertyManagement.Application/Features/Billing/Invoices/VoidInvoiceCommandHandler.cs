using System.Data;
using MediatR;
using PropertyManagement.Application.Abstractions;
using PropertyManagement.Application.Abstractions.Billing;
using PropertyManagement.Application.Common.Enums;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Application.Features.Billing.Responses;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Application.Features.Billing.Invoices;

public sealed class VoidInvoiceCommandHandler(
    IInvoiceRepository invoiceRepository,
    IChargeRepository chargeRepository,
    IUnitOfWork unitOfWork,
    IClock clock) : IRequestHandler<VoidInvoiceCommand, Result<InvoiceResponse>>
{
    public async Task<Result<InvoiceResponse>> Handle(VoidInvoiceCommand request, CancellationToken cancellationToken)
    {
        await using var transaction = await unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

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

        var utcNow = clock.UtcNow;

        try
        {
            invoice.Void(request.Reason, utcNow);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return Result<InvoiceResponse>.Failure(new Error(
                Code: "Invoice.VoidError",
                Message: ex.Message,
                Type: ErrorType.Conflict));
        }

        var linkedCharges = await chargeRepository.GetByInvoiceIdAsync(invoice.Id, request.OrganizationId, cancellationToken);
        foreach (var charge in linkedCharges)
        {
            if (charge.Status == ChargeStatus.Billed)
            {
                charge.ReleaseToPending(utcNow);
                chargeRepository.Update(charge);
            }
        }

        invoiceRepository.Update(invoice);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result<InvoiceResponse>.Success(InvoiceResponse.FromEntity(invoice));
    }
}
