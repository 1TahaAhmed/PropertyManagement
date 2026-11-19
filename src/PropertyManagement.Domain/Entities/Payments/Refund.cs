using PropertyManagement.Domain.Common;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Domain.Entities.Payments;

public sealed class Refund : IOrganizationScoped
{
    private Refund() { }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid PaymentId { get; private set; }
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = CurrencyCode.Default;
    public RefundStatus Status { get; private set; }
    public string? ProviderRefundId { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public string? IdempotencyKey { get; private set; }
    public string? FailureReason { get; private set; }
    public Guid? RequestedByUserId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    internal static Refund Create(
        Guid organizationId,
        Guid paymentId,
        decimal amount,
        string currency,
        string reason,
        DateTime utcNow,
        Guid? requestedByUserId = null,
        string? idempotencyKey = null)
    {
        if (organizationId == Guid.Empty) throw new ArgumentException("Organization ID cannot be empty.", nameof(organizationId));
        if (paymentId == Guid.Empty) throw new ArgumentException("Payment ID cannot be empty.", nameof(paymentId));
        ArgumentException.ThrowIfNullOrWhiteSpace(reason, nameof(reason));

        var roundedAmount = MoneyRounding.Round(amount);
        if (roundedAmount <= 0m)
        {
            throw new ArgumentException("Refund amount must be greater than zero.", nameof(amount));
        }

        return new Refund
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            PaymentId = paymentId,
            Amount = roundedAmount,
            Currency = CurrencyCode.Normalize(currency),
            Status = RefundStatus.Pending,
            Reason = reason.Trim(),
            RequestedByUserId = requestedByUserId,
            IdempotencyKey = string.IsNullOrWhiteSpace(idempotencyKey) ? null : idempotencyKey.Trim(),
            CreatedAtUtc = utcNow
        };
    }

    internal void MarkProcessing(string? providerRefundId)
    {
        if (Status != RefundStatus.Pending)
        {
            throw new InvalidOperationException($"Cannot move refund from '{Status}' to '{RefundStatus.Processing}'.");
        }

        Status = RefundStatus.Processing;
        if (!string.IsNullOrWhiteSpace(providerRefundId))
        {
            ProviderRefundId = providerRefundId.Trim();
        }
    }

    internal void MarkSucceeded(string? providerRefundId, DateTime utcNow)
    {
        if (Status == RefundStatus.Succeeded)
        {
            return;
        }

        if (Status is not (RefundStatus.Pending or RefundStatus.Processing))
        {
            throw new InvalidOperationException($"Cannot mark refund in '{Status}' status as Succeeded.");
        }

        Status = RefundStatus.Succeeded;
        if (!string.IsNullOrWhiteSpace(providerRefundId))
        {
            ProviderRefundId = providerRefundId.Trim();
        }

        FailureReason = null;
        CompletedAtUtc = utcNow;
    }

    internal void MarkFailed(string failureReason, DateTime utcNow)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(failureReason, nameof(failureReason));
        if (Status is RefundStatus.Succeeded or RefundStatus.Failed)
        {
            throw new InvalidOperationException($"Cannot mark refund in '{Status}' status as Failed.");
        }

        Status = RefundStatus.Failed;
        FailureReason = failureReason.Trim();
        CompletedAtUtc = utcNow;
    }
}
