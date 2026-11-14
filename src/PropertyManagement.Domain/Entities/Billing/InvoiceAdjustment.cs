using PropertyManagement.Domain.Common;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Domain.Entities.Billing;

public sealed class InvoiceAdjustment : IOrganizationScoped
{
    private InvoiceAdjustment() { }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid InvoiceId { get; private set; }
    public InvoiceAdjustmentType Type { get; private set; }
    public ChargeType ChargeType { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public string Reason { get; private set; } = string.Empty;
    public decimal Amount { get; private set; }
    public decimal SignedAmount => Type == InvoiceAdjustmentType.Credit ? -Amount : Amount;
    public Guid CreatedByUserId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    internal static InvoiceAdjustment Create(
        Guid organizationId,
        Guid invoiceId,
        InvoiceAdjustmentType type,
        ChargeType chargeType,
        decimal amount,
        string description,
        string reason,
        Guid createdByUserId,
        DateTime utcNow)
    {
        if (organizationId == Guid.Empty) throw new ArgumentException("Organization ID cannot be empty.", nameof(organizationId));
        if (invoiceId == Guid.Empty) throw new ArgumentException("Invoice ID cannot be empty.", nameof(invoiceId));
        if (!Enum.IsDefined(type)) throw new ArgumentOutOfRangeException(nameof(type), "Invalid adjustment type.");
        if (!Enum.IsDefined(chargeType)) throw new ArgumentOutOfRangeException(nameof(chargeType), "Invalid charge type.");
        if (createdByUserId == Guid.Empty) throw new ArgumentException("Actor user ID cannot be empty.", nameof(createdByUserId));
        ArgumentException.ThrowIfNullOrWhiteSpace(description, nameof(description));
        ArgumentException.ThrowIfNullOrWhiteSpace(reason, nameof(reason));

        if (type == InvoiceAdjustmentType.Credit && chargeType is not (ChargeType.CreditAdjustment or ChargeType.Discount))
        {
            throw new ArgumentException($"Credit adjustments must use CreditAdjustment or Discount charge type, not '{chargeType}'.", nameof(chargeType));
        }

        if (type == InvoiceAdjustmentType.Debit && chargeType is ChargeType.Rent or ChargeType.Discount or ChargeType.CreditAdjustment)
        {
            throw new ArgumentException($"Debit adjustments cannot use '{chargeType}' charge type.", nameof(chargeType));
        }

        var roundedAmount = MoneyRounding.Round(amount);
        if (roundedAmount <= 0)
        {
            throw new ArgumentException("Adjustment amount must be greater than zero.", nameof(amount));
        }

        return new InvoiceAdjustment
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            InvoiceId = invoiceId,
            Type = type,
            ChargeType = chargeType,
            Description = description.Trim(),
            Reason = reason.Trim(),
            Amount = roundedAmount,
            CreatedByUserId = createdByUserId,
            CreatedAtUtc = utcNow
        };
    }
}
