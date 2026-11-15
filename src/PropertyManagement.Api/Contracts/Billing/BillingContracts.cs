using PropertyManagement.Application.Features.Billing.Common;
using PropertyManagement.Application.Features.Billing.Invoices;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Api.Contracts.Billing;

public sealed record PreviewBillingRequest(
    Guid LeaseId,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    DateOnly? IssueDate = null,
    DateOnly? DueDateOverride = null,
    bool IncludeRent = true,
    bool IncludePendingCharges = true,
    IReadOnlyCollection<Guid>? ChargeIds = null,
    IReadOnlyList<BillingDiscountRequest>? Discounts = null,
    IReadOnlyList<BillingAdditionalChargeRequest>? AdditionalCharges = null);

public sealed record GenerateLeaseInvoiceRequest(
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
    string? IdempotencyKey = null);

public sealed record CreateChargeRequest(
    Guid LeaseId,
    ChargeType Type,
    string Description,
    decimal Quantity,
    decimal UnitPrice,
    DateOnly? PeriodStart = null,
    DateOnly? PeriodEnd = null);

public sealed record VoidChargeRequest(string Reason);

public sealed record CreateManualInvoiceRequest(
    Guid LeaseId,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    DateOnly DueDate,
    IReadOnlyList<ManualInvoiceLineInput>? Lines = null,
    IReadOnlyCollection<Guid>? ChargeIds = null,
    DateOnly? IssueDate = null,
    string? Notes = null,
    bool AutoIssue = false,
    string? IdempotencyKey = null);

public sealed record IssueInvoiceRequest(
    DateOnly? IssueDate = null,
    string? RowVersion = null);

public sealed record VoidInvoiceRequest(
    string Reason,
    string? RowVersion = null);

public sealed record ApplyInvoiceAdjustmentRequest(
    InvoiceAdjustmentType Type,
    ChargeType ChargeType,
    decimal Amount,
    string Description,
    string Reason,
    string? RowVersion = null);

public sealed record MarkOverdueInvoicesRequest(DateOnly? AsOfDate = null);
