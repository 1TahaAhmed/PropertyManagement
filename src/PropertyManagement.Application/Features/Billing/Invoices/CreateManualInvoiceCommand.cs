using MediatR;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Application.Features.Billing.Responses;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Application.Features.Billing.Invoices;

public sealed record ManualInvoiceLineInput(
    ChargeType Type,
    string Description,
    decimal Quantity,
    decimal UnitPrice,
    DateOnly? PeriodStart = null,
    DateOnly? PeriodEnd = null);

public sealed record CreateManualInvoiceCommand(
    Guid OrganizationId,
    Guid LeaseId,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    DateOnly DueDate,
    IReadOnlyList<ManualInvoiceLineInput>? Lines = null,
    IReadOnlyCollection<Guid>? ChargeIds = null,
    DateOnly? IssueDate = null,
    string? Notes = null,
    bool AutoIssue = false,
    string? IdempotencyKey = null) : IRequest<Result<InvoiceResponse>>;
