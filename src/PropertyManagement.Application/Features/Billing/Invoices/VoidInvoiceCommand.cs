using MediatR;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Application.Features.Billing.Responses;

namespace PropertyManagement.Application.Features.Billing.Invoices;

public sealed record VoidInvoiceCommand(
    Guid OrganizationId,
    Guid InvoiceId,
    string Reason,
    string? RowVersion = null) : IRequest<Result<InvoiceResponse>>;
