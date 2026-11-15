using PropertyManagement.Domain.Common;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Domain.Entities.Billing;

public sealed class Charge : IOrganizationScoped
{
    private Charge() { }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid LeaseId { get; private set; }
    public Guid PropertyId { get; private set; }
    public Guid UnitId { get; private set; }
    public Guid ResidentId { get; private set; }
    public ChargeType Type { get; private set; }
    public ChargeStatus Status { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public decimal Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = CurrencyCode.Default;
    public DateOnly? PeriodStart { get; private set; }
    public DateOnly? PeriodEnd { get; private set; }
    public Guid? InvoiceId { get; private set; }
    public string? VoidReason { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public static bool IsCreditType(ChargeType type) =>
        type is ChargeType.Discount or ChargeType.CreditAdjustment;

    public static Charge Create(
        Guid organizationId,
        Guid leaseId,
        Guid propertyId,
        Guid unitId,
        Guid residentId,
        ChargeType type,
        string description,
        decimal quantity,
        decimal unitPrice,
        string currency,
        DateTime utcNow,
        DateOnly? periodStart = null,
        DateOnly? periodEnd = null)
    {
        if (organizationId == Guid.Empty) throw new ArgumentException("Organization ID cannot be empty.", nameof(organizationId));
        if (leaseId == Guid.Empty) throw new ArgumentException("Lease ID cannot be empty.", nameof(leaseId));
        if (propertyId == Guid.Empty) throw new ArgumentException("Property ID cannot be empty.", nameof(propertyId));
        if (unitId == Guid.Empty) throw new ArgumentException("Unit ID cannot be empty.", nameof(unitId));
        if (residentId == Guid.Empty) throw new ArgumentException("Resident ID cannot be empty.", nameof(residentId));
        if (!Enum.IsDefined(type)) throw new ArgumentOutOfRangeException(nameof(type), "Invalid charge type.");
        ArgumentException.ThrowIfNullOrWhiteSpace(description, nameof(description));
        if (quantity <= 0) throw new ArgumentException("Charge quantity must be greater than zero.", nameof(quantity));
        if (unitPrice <= 0) throw new ArgumentException("Charge unit price must be greater than zero.", nameof(unitPrice));

        if (periodStart.HasValue != periodEnd.HasValue)
        {
            throw new ArgumentException("PeriodStart and PeriodEnd must either both be provided or both be omitted.");
        }

        if (periodStart.HasValue && periodEnd.HasValue && periodStart.Value > periodEnd.Value)
        {
            throw new ArgumentException("PeriodStart cannot be later than PeriodEnd.");
        }

        var normalizedCurrency = CurrencyCode.Normalize(currency);
        var roundedMagnitude = MoneyRounding.Round(quantity * unitPrice);
        if (roundedMagnitude <= 0)
        {
            throw new ArgumentException("Charge total amount must be greater than zero after rounding.");
        }

        var signedAmount = IsCreditType(type) ? -roundedMagnitude : roundedMagnitude;

        return new Charge
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            LeaseId = leaseId,
            PropertyId = propertyId,
            UnitId = unitId,
            ResidentId = residentId,
            Type = type,
            Status = ChargeStatus.Pending,
            Description = description.Trim(),
            Quantity = quantity,
            UnitPrice = MoneyRounding.Round(unitPrice),
            Amount = signedAmount,
            Currency = normalizedCurrency,
            PeriodStart = periodStart,
            PeriodEnd = periodEnd,
            CreatedAtUtc = utcNow
        };
    }

    public void MarkBilled(Guid invoiceId, DateTime utcNow)
    {
        if (invoiceId == Guid.Empty) throw new ArgumentException("Invoice ID cannot be empty.", nameof(invoiceId));
        if (Status != ChargeStatus.Pending)
        {
            throw new InvalidOperationException($"Cannot bill charge in '{Status}' status. Only Pending charges can be billed.");
        }

        Status = ChargeStatus.Billed;
        InvoiceId = invoiceId;
        UpdatedAtUtc = utcNow;
    }

    public void ReleaseToPending(DateTime utcNow)
    {
        if (Status != ChargeStatus.Billed)
        {
            throw new InvalidOperationException($"Cannot release charge in '{Status}' status. Only Billed charges can be released back to Pending.");
        }

        Status = ChargeStatus.Pending;
        InvoiceId = null;
        UpdatedAtUtc = utcNow;
    }

    public void Void(string reason, DateTime utcNow)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason, nameof(reason));
        if (Status != ChargeStatus.Pending)
        {
            throw new InvalidOperationException($"Cannot void charge in '{Status}' status. Only Pending charges can be voided directly.");
        }

        Status = ChargeStatus.Voided;
        VoidReason = reason.Trim();
        UpdatedAtUtc = utcNow;
    }
}
