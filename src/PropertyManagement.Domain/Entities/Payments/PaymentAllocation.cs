using PropertyManagement.Domain.Common;

namespace PropertyManagement.Domain.Entities.Payments;

public sealed class PaymentAllocation : IOrganizationScoped
{
    private PaymentAllocation() { }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid PaymentId { get; private set; }
    public Guid InvoiceId { get; private set; }
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = CurrencyCode.Default;
    public DateTime AllocatedAtUtc { get; private set; }
    public Guid? AllocatedByUserId { get; private set; }
    public bool IsReversed { get; private set; }
    public DateTime? ReversedAtUtc { get; private set; }
    public string? ReversalReason { get; private set; }
    public Guid? ReversedByUserId { get; private set; }

    internal static PaymentAllocation Create(
        Guid organizationId,
        Guid paymentId,
        Guid invoiceId,
        decimal amount,
        string currency,
        DateTime utcNow,
        Guid? allocatedByUserId = null)
    {
        if (organizationId == Guid.Empty) throw new ArgumentException("Organization ID cannot be empty.", nameof(organizationId));
        if (paymentId == Guid.Empty) throw new ArgumentException("Payment ID cannot be empty.", nameof(paymentId));
        if (invoiceId == Guid.Empty) throw new ArgumentException("Invoice ID cannot be empty.", nameof(invoiceId));

        var roundedAmount = MoneyRounding.Round(amount);
        if (roundedAmount <= 0m)
        {
            throw new ArgumentException("Allocation amount must be greater than zero.", nameof(amount));
        }

        return new PaymentAllocation
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            PaymentId = paymentId,
            InvoiceId = invoiceId,
            Amount = roundedAmount,
            Currency = CurrencyCode.Normalize(currency),
            AllocatedAtUtc = utcNow,
            AllocatedByUserId = allocatedByUserId,
            IsReversed = false
        };
    }

    internal void MarkReversed(string reason, DateTime utcNow, Guid? reversedByUserId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason, nameof(reason));
        if (IsReversed)
        {
            throw new InvalidOperationException("Payment allocation has already been reversed.");
        }

        IsReversed = true;
        ReversedAtUtc = utcNow;
        ReversalReason = reason.Trim();
        ReversedByUserId = reversedByUserId;
    }
}
