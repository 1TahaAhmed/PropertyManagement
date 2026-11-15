using MediatR;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Application.Features.Billing.Responses;

namespace PropertyManagement.Application.Features.Billing.Invoices;

public sealed record IssueInvoiceCommand(
    Guid OrganizationId,
    Guid InvoiceId,
    DateOnly? IssueDate = null,
    string? RowVersion = null) : IRequest<Result<InvoiceResponse>>;
