using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Application.Features.Billing.Common;

public sealed record BillingDiscountRequest(
    string Description,
    decimal Amount);

public sealed record BillingAdditionalChargeRequest(
    ChargeType Type,
    string Description,
    decimal Quantity,
    decimal UnitPrice,
    DateOnly? PeriodStart = null,
    DateOnly? PeriodEnd = null);
