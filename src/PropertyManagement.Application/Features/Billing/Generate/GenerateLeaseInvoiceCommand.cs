using MediatR;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Application.Features.Billing.Common;
using PropertyManagement.Application.Features.Billing.Responses;

namespace PropertyManagement.Application.Features.Billing.Generate;

public sealed record GenerateLeaseInvoiceCommand(
    Guid OrganizationId,
    Guid LeaseId,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    DateOnly? IssueDate = null,
    DateOnly? DueDateOverride = null,
    bool IncludeRent = true,
    bool IncludePendingCharges = true,
    IReadOnlyCollection<Guid>? ChargeIds = null,
    IReadOnlyList<BillingDiscountRequest>? Discounts = null,
    IReadOnlyList<BillingAdditionalChargeRequest>? AdditionalCharges = null,
    string? Notes = null,
    bool AutoIssue = false,
    string? IdempotencyKey = null) : IRequest<Result<InvoiceResponse>>;
