using PropertyManagement.Domain.Entities.Billing;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Application.Features.Billing.Responses;

public sealed record InvoiceLineResponse(
    Guid Id,
    ChargeType Type,
    string Description,
    decimal Quantity,
    decimal UnitPrice,
    decimal Amount,
    Guid? SourceChargeId,
    DateOnly? PeriodStart,
    DateOnly? PeriodEnd,
    DateTime CreatedAtUtc)
{
    public static InvoiceLineResponse FromEntity(InvoiceLine line) => new(
        line.Id,
        line.Type,
        line.Description,
        line.Quantity,
        line.UnitPrice,
        line.Amount,
        line.SourceChargeId,
        line.PeriodStart,
        line.PeriodEnd,
        line.CreatedAtUtc);
}

public sealed record InvoiceAdjustmentResponse(
    Guid Id,
    InvoiceAdjustmentType Type,
    ChargeType ChargeType,
    string Description,
    string Reason,
    decimal Amount,
    decimal SignedAmount,
    Guid CreatedByUserId,
    DateTime CreatedAtUtc)
{
    public static InvoiceAdjustmentResponse FromEntity(InvoiceAdjustment adjustment) => new(
        adjustment.Id,
        adjustment.Type,
        adjustment.ChargeType,
        adjustment.Description,
        adjustment.Reason,
        adjustment.Amount,
        adjustment.SignedAmount,
        adjustment.CreatedByUserId,
        adjustment.CreatedAtUtc);
}

public sealed record InvoiceResponse(
    Guid Id,
    Guid OrganizationId,
    Guid LeaseId,
    Guid PropertyId,
    Guid UnitId,
    Guid ResidentId,
    string? InvoiceNumber,
    DateOnly BillingPeriodStart,
    DateOnly BillingPeriodEnd,
    DateOnly IssueDate,
    DateOnly DueDate,
    string Currency,
    InvoiceStatus Status,
    decimal Subtotal,
    decimal TotalAdjustments,
    decimal Total,
    decimal AmountPaid,
    decimal BalanceDue,
    string? Notes,
    string? VoidReason,
    DateTime? IssuedAtUtc,
    DateTime? VoidedAtUtc,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    string RowVersion,
    IReadOnlyList<InvoiceLineResponse> Lines,
    IReadOnlyList<InvoiceAdjustmentResponse> Adjustments)
{
    public static InvoiceResponse FromEntity(Invoice invoice) => new(
        invoice.Id,
        invoice.OrganizationId,
        invoice.LeaseId,
        invoice.PropertyId,
        invoice.UnitId,
        invoice.ResidentId,
        invoice.InvoiceNumber,
        invoice.BillingPeriodStart,
        invoice.BillingPeriodEnd,
        invoice.IssueDate,
        invoice.DueDate,
        invoice.Currency,
        invoice.Status,
        invoice.Subtotal,
        invoice.TotalAdjustments,
        invoice.Total,
        invoice.AmountPaid,
        invoice.BalanceDue,
        invoice.Notes,
        invoice.VoidReason,
        invoice.IssuedAtUtc,
        invoice.VoidedAtUtc,
        invoice.CreatedAtUtc,
        invoice.UpdatedAtUtc,
        Convert.ToBase64String(invoice.RowVersion),
        invoice.Lines.Select(InvoiceLineResponse.FromEntity).ToList(),
        invoice.Adjustments.Select(InvoiceAdjustmentResponse.FromEntity).ToList());
}

public sealed record InvoiceSummaryResponse(
    Guid Id,
    Guid OrganizationId,
    Guid LeaseId,
    Guid PropertyId,
    Guid UnitId,
    Guid ResidentId,
    string? InvoiceNumber,
    DateOnly BillingPeriodStart,
    DateOnly BillingPeriodEnd,
    DateOnly IssueDate,
    DateOnly DueDate,
    string Currency,
    InvoiceStatus Status,
    decimal Subtotal,
    decimal TotalAdjustments,
    decimal Total,
    decimal AmountPaid,
    decimal BalanceDue,
    DateTime? IssuedAtUtc,
    DateTime CreatedAtUtc,
    string RowVersion)
{
    public static InvoiceSummaryResponse FromEntity(Invoice invoice) => new(
        invoice.Id,
        invoice.OrganizationId,
        invoice.LeaseId,
        invoice.PropertyId,
        invoice.UnitId,
        invoice.ResidentId,
        invoice.InvoiceNumber,
        invoice.BillingPeriodStart,
        invoice.BillingPeriodEnd,
        invoice.IssueDate,
        invoice.DueDate,
        invoice.Currency,
        invoice.Status,
        invoice.Subtotal,
        invoice.TotalAdjustments,
        invoice.Total,
        invoice.AmountPaid,
        invoice.BalanceDue,
        invoice.IssuedAtUtc,
        invoice.CreatedAtUtc,
        Convert.ToBase64String(invoice.RowVersion));
}

public sealed record PagedInvoiceResponse(
    IReadOnlyList<InvoiceSummaryResponse> Items,
    int TotalCount,
    int PageNumber,
    int PageSize);

public sealed record ChargeResponse(
    Guid Id,
    Guid OrganizationId,
    Guid LeaseId,
    Guid PropertyId,
    Guid UnitId,
    Guid ResidentId,
    ChargeType Type,
    ChargeStatus Status,
    string Description,
    decimal Quantity,
    decimal UnitPrice,
    decimal Amount,
    string Currency,
    DateOnly? PeriodStart,
    DateOnly? PeriodEnd,
    Guid? InvoiceId,
    string? VoidReason,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc)
{
    public static ChargeResponse FromEntity(Charge charge) => new(
        charge.Id,
        charge.OrganizationId,
        charge.LeaseId,
        charge.PropertyId,
        charge.UnitId,
        charge.ResidentId,
        charge.Type,
        charge.Status,
        charge.Description,
        charge.Quantity,
        charge.UnitPrice,
        charge.Amount,
        charge.Currency,
        charge.PeriodStart,
        charge.PeriodEnd,
        charge.InvoiceId,
        charge.VoidReason,
        charge.CreatedAtUtc,
        charge.UpdatedAtUtc);
}

public sealed record BillingPreviewLineResponse(
    ChargeType Type,
    string Description,
    decimal Quantity,
    decimal UnitPrice,
    decimal Amount,
    DateOnly? PeriodStart,
    DateOnly? PeriodEnd,
    bool IsProrated,
    int? OccupiedDays,
    int? DaysInMonth,
    Guid? SourceChargeId);

public sealed record BillingPreviewResponse(
    Guid LeaseId,
    Guid ResidentId,
    DateOnly BillingPeriodStart,
    DateOnly BillingPeriodEnd,
    DateOnly EffectiveStart,
    DateOnly EffectiveEnd,
    bool IsProrated,
    DateOnly IssueDate,
    DateOnly DueDate,
    string Currency,
    decimal Subtotal,
    decimal Total,
    IReadOnlyList<BillingPreviewLineResponse> Lines,
    bool AlreadyBilledForPeriod);

public sealed record MarkOverdueInvoicesResponse(
    int MarkedCount,
    IReadOnlyList<Guid> MarkedInvoiceIds);
