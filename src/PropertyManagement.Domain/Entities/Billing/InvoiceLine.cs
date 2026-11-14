using PropertyManagement.Domain.Common;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Domain.Entities.Billing;

public sealed class InvoiceLine : IOrganizationScoped
{
    private InvoiceLine() { }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid InvoiceId { get; private set; }
    public ChargeType Type { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public decimal Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }
    public decimal Amount { get; private set; }
    public Guid? SourceChargeId { get; private set; }
    public DateOnly? PeriodStart { get; private set; }
    public DateOnly? PeriodEnd { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    internal static InvoiceLine Create(
        Guid organizationId,
        Guid invoiceId,
        ChargeType type,
        string description,
        decimal quantity,
        decimal unitPrice,
        DateTime utcNow,
        Guid? sourceChargeId = null,
        DateOnly? periodStart = null,
        DateOnly? periodEnd = null)
    {
        if (organizationId == Guid.Empty) throw new ArgumentException("Organization ID cannot be empty.", nameof(organizationId));
        if (invoiceId == Guid.Empty) throw new ArgumentException("Invoice ID cannot be empty.", nameof(invoiceId));
        if (!Enum.IsDefined(type)) throw new ArgumentOutOfRangeException(nameof(type), "Invalid charge type.");
        ArgumentException.ThrowIfNullOrWhiteSpace(description, nameof(description));
        if (quantity <= 0) throw new ArgumentException("Line quantity must be greater than zero.", nameof(quantity));
        if (unitPrice <= 0) throw new ArgumentException("Line unit price must be greater than zero.", nameof(unitPrice));

        if (periodStart.HasValue != periodEnd.HasValue)
        {
            throw new ArgumentException("PeriodStart and PeriodEnd must either both be provided or both be omitted.");
        }

        if (periodStart.HasValue && periodEnd.HasValue && periodStart.Value > periodEnd.Value)
        {
            throw new ArgumentException("PeriodStart cannot be later than PeriodEnd.");
        }

        var roundedUnitPrice = MoneyRounding.Round(unitPrice);
        var roundedMagnitude = MoneyRounding.Round(quantity * unitPrice);
        if (roundedMagnitude <= 0)
        {
            throw new ArgumentException("Line amount must be greater than zero after rounding.");
        }

        var signedAmount = Charge.IsCreditType(type) ? -roundedMagnitude : roundedMagnitude;

        return new InvoiceLine
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            InvoiceId = invoiceId,
            Type = type,
            Description = description.Trim(),
            Quantity = quantity,
            UnitPrice = roundedUnitPrice,
            Amount = signedAmount,
            SourceChargeId = sourceChargeId == Guid.Empty ? null : sourceChargeId,
            PeriodStart = periodStart,
            PeriodEnd = periodEnd,
            CreatedAtUtc = utcNow
        };
    }
}
