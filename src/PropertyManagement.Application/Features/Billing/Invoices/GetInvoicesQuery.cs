using MediatR;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Application.Features.Billing.Responses;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Application.Features.Billing.Invoices;

public sealed record GetInvoicesQuery(
    Guid OrganizationId,
    Guid? LeaseId = null,
    Guid? ResidentId = null,
    Guid? PropertyId = null,
    Guid? UnitId = null,
    InvoiceStatus? Status = null,
    bool OnlyOverdue = false,
    DateOnly? DueBefore = null,
    DateOnly? PeriodStart = null,
    DateOnly? PeriodEnd = null,
    string? InvoiceNumber = null,
    int PageNumber = 1,
    int PageSize = 20) : IRequest<Result<PagedInvoiceResponse>>;
