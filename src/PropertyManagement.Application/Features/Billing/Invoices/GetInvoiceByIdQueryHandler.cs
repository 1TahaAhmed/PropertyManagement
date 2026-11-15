using MediatR;
using PropertyManagement.Application.Abstractions.Billing;
using PropertyManagement.Application.Common.Enums;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Application.Features.Billing.Responses;

namespace PropertyManagement.Application.Features.Billing.Invoices;

public sealed class GetInvoiceByIdQueryHandler(
    IInvoiceRepository invoiceRepository) : IRequestHandler<GetInvoiceByIdQuery, Result<InvoiceResponse>>
{
    public async Task<Result<InvoiceResponse>> Handle(GetInvoiceByIdQuery request, CancellationToken cancellationToken)
    {
        var invoice = await invoiceRepository.GetByIdAsync(request.InvoiceId, request.OrganizationId, cancellationToken);
        if (invoice is null || invoice.OrganizationId != request.OrganizationId)
        {
            return Result<InvoiceResponse>.Failure(new Error(
                Code: "Invoice.NotFound",
                Message: "The specified invoice was not found.",
                Type: ErrorType.NotFound));
        }

        return Result<InvoiceResponse>.Success(InvoiceResponse.FromEntity(invoice));
    }
}
