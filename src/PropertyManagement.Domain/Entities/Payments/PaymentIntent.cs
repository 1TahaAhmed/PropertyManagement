using PropertyManagement.Domain.Common;
using PropertyManagement.Domain.Entities.Payments.Events;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Domain.Entities.Payments;

public sealed class PaymentIntent : IOrganizationScoped, IHasDomainEvents
{
    private readonly List<IDomainEvent> _domainEvents = [];

    private PaymentIntent() { }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid ResidentId { get; private set; }
    public Guid? LeaseId { get; private set; }
    public Guid? TargetInvoiceId { get; private set; }
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = CurrencyCode.Default;
    public PaymentMethod PaymentMethod { get; private set; }
    public PaymentProviderType Provider { get; private set; }
    public PaymentIntentStatus Status { get; private set; }
    public string? ProviderIntentId { get; private set; }
    public string? ClientSecret { get; private set; }
    public string? ActionUrl { get; private set; }
    public string? IdempotencyKey { get; private set; }
    public string? FailureReason { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();
    public void ClearDomainEvents() => _domainEvents.Clear();

    public static PaymentIntent Create(
        Guid organizationId,
        Guid residentId,
        decimal amount,
        string currency,
        PaymentMethod paymentMethod,
        PaymentProviderType provider,
        DateTime expiresAtUtc,
        DateTime utcNow,
        Guid? leaseId = null,
        Guid? targetInvoiceId = null,
        string? idempotencyKey = null)
    {
        if (organizationId == Guid.Empty) throw new ArgumentException("Organization ID cannot be empty.", nameof(organizationId));
        if (residentId == Guid.Empty) throw new ArgumentException("Resident ID cannot be empty.", nameof(residentId));
        if (leaseId == Guid.Empty) throw new ArgumentException("Lease ID cannot be empty when specified.", nameof(leaseId));
        if (targetInvoiceId == Guid.Empty) throw new ArgumentException("Target Invoice ID cannot be empty when specified.", nameof(targetInvoiceId));

        var roundedAmount = MoneyRounding.Round(amount);
        if (roundedAmount <= 0m)
        {
            throw new ArgumentException("Payment intent amount must be greater than zero.", nameof(amount));
        }

        if (expiresAtUtc <= utcNow)
        {
            throw new ArgumentException("Payment intent expiration must be in the future.", nameof(expiresAtUtc));
        }

        var intent = new PaymentIntent
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            ResidentId = residentId,
            LeaseId = leaseId,
            TargetInvoiceId = targetInvoiceId,
            Amount = roundedAmount,
            Currency = CurrencyCode.Normalize(currency),
            PaymentMethod = paymentMethod,
            Provider = provider,
            Status = PaymentIntentStatus.Pending,
            IdempotencyKey = string.IsNullOrWhiteSpace(idempotencyKey) ? null : idempotencyKey.Trim(),
            ExpiresAtUtc = expiresAtUtc,
            CreatedAtUtc = utcNow
        };

        intent._domainEvents.Add(new PaymentIntentCreatedEvent(
            intent.Id,
            intent.OrganizationId,
            intent.ResidentId,
            intent.Amount,
            intent.Currency,
            intent.Provider,
            intent.PaymentMethod));

        return intent;
    }

    public void AttachProviderSession(
        string providerIntentId,
        string? clientSecret,
        string? actionUrl,
        bool requiresAction,
        DateTime utcNow)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerIntentId, nameof(providerIntentId));
        if (Status is not (PaymentIntentStatus.Pending or PaymentIntentStatus.RequiresAction))
        {
            throw new InvalidOperationException($"Cannot attach provider session when intent is in '{Status}' status.");
        }

        ProviderIntentId = providerIntentId.Trim();
        ClientSecret = string.IsNullOrWhiteSpace(clientSecret) ? null : clientSecret.Trim();
        ActionUrl = string.IsNullOrWhiteSpace(actionUrl) ? null : actionUrl.Trim();
        Status = requiresAction ? PaymentIntentStatus.RequiresAction : PaymentIntentStatus.Processing;
        UpdatedAtUtc = utcNow;
    }

    public void MarkProcessing(DateTime utcNow)
    {
        if (Status is not (PaymentIntentStatus.Pending or PaymentIntentStatus.RequiresAction))
        {
            throw new InvalidOperationException($"Cannot move payment intent from '{Status}' to '{PaymentIntentStatus.Processing}'.");
        }

        Status = PaymentIntentStatus.Processing;
        UpdatedAtUtc = utcNow;
    }

    public void MarkSucceeded(DateTime utcNow)
    {
        if (Status == PaymentIntentStatus.Succeeded)
        {
            return;
        }

        if (Status is not (PaymentIntentStatus.Pending or PaymentIntentStatus.RequiresAction or PaymentIntentStatus.Processing))
        {
            throw new InvalidOperationException($"Cannot mark payment intent in '{Status}' status as Succeeded.");
        }

        Status = PaymentIntentStatus.Succeeded;
        FailureReason = null;
        UpdatedAtUtc = utcNow;
    }

    public void MarkFailed(string reason, DateTime utcNow)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason, nameof(reason));
        if (Status is PaymentIntentStatus.Succeeded or PaymentIntentStatus.Cancelled or PaymentIntentStatus.Expired)
        {
            throw new InvalidOperationException($"Cannot mark payment intent in '{Status}' status as Failed.");
        }

        Status = PaymentIntentStatus.Failed;
        FailureReason = reason.Trim();
        UpdatedAtUtc = utcNow;
    }

    public void Cancel(string reason, DateTime utcNow)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason, nameof(reason));
        if (Status is PaymentIntentStatus.Succeeded or PaymentIntentStatus.Failed or PaymentIntentStatus.Expired)
        {
            throw new InvalidOperationException($"Cannot cancel payment intent in '{Status}' status.");
        }

        if (Status == PaymentIntentStatus.Cancelled)
        {
            return;
        }

        Status = PaymentIntentStatus.Cancelled;
        FailureReason = reason.Trim();
        UpdatedAtUtc = utcNow;
    }

    public void ExpireIfPastDue(DateTime utcNow)
    {
        if (Status is not (PaymentIntentStatus.Pending or PaymentIntentStatus.RequiresAction))
        {
            return;
        }

        if (utcNow < ExpiresAtUtc)
        {
            throw new InvalidOperationException($"Payment intent does not expire until {ExpiresAtUtc:O}.");
        }

        Status = PaymentIntentStatus.Expired;
        UpdatedAtUtc = utcNow;
    }
}
