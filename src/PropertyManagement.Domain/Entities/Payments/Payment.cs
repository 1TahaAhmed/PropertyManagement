using PropertyManagement.Domain.Common;
using PropertyManagement.Domain.Entities.Payments.Events;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Domain.Entities.Payments;

public sealed class Payment : IOrganizationScoped, IHasDomainEvents
{
    private readonly List<PaymentAllocation> _allocations = [];
    private readonly List<Refund> _refunds = [];
    private readonly List<IDomainEvent> _domainEvents = [];

    private Payment() { }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid ResidentId { get; private set; }
    public Guid? LeaseId { get; private set; }
    public Guid? PaymentIntentId { get; private set; }
    public string? PaymentNumber { get; private set; }
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = CurrencyCode.Default;
    public decimal ProviderFee { get; private set; }
    public decimal NetSettlementAmount { get; private set; }
    public decimal AllocatedAmount { get; private set; }
    public decimal UnappliedAmount { get; private set; }
    public decimal RefundedAmount { get; private set; }
    public PaymentMethod PaymentMethod { get; private set; }
    public PaymentProviderType Provider { get; private set; }
    public PaymentStatus Status { get; private set; }
    public OverpaymentPolicy OverpaymentPolicy { get; private set; }
    public string? ProviderPaymentId { get; private set; }
    public string? ProviderCustomerId { get; private set; }
    public string? ProviderTransactionId { get; private set; }
    public string? ProviderStatus { get; private set; }
    public string? Reference { get; private set; }
    public string? FailureReason { get; private set; }
    public string? IdempotencyKey { get; private set; }
    public string? MetadataJson { get; private set; }
    public Guid? RecordedByUserId { get; private set; }
    public Guid? ConfirmedByUserId { get; private set; }
    public DateTime? ReceivedAtUtc { get; private set; }
    public DateTime? ConfirmedAtUtc { get; private set; }
    public DateTime? SettledAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public IReadOnlyCollection<PaymentAllocation> Allocations => _allocations.AsReadOnly();
    public IReadOnlyCollection<Refund> Refunds => _refunds.AsReadOnly();
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    public decimal RemainingRefundableAmount => MoneyRounding.Round(Amount - RefundedAmount);
    public decimal PendingRefundAmount => MoneyRounding.Round(
        _refunds.Where(r => r.Status is RefundStatus.Pending or RefundStatus.Processing).Sum(r => r.Amount));
    public decimal AvailableToRefundWithoutReversingAllocations =>
        MoneyRounding.Round(UnappliedAmount - PendingRefundAmount);

    public void ClearDomainEvents() => _domainEvents.Clear();

    public static Payment Create(
        Guid organizationId,
        Guid residentId,
        decimal amount,
        string currency,
        PaymentMethod paymentMethod,
        PaymentProviderType provider,
        DateTime utcNow,
        Guid? leaseId = null,
        Guid? paymentIntentId = null,
        OverpaymentPolicy overpaymentPolicy = OverpaymentPolicy.KeepUnappliedCredit,
        string? idempotencyKey = null,
        string? reference = null,
        string? metadataJson = null,
        Guid? recordedByUserId = null,
        DateTime? receivedAtUtc = null)
    {
        if (organizationId == Guid.Empty) throw new ArgumentException("Organization ID cannot be empty.", nameof(organizationId));
        if (residentId == Guid.Empty) throw new ArgumentException("Resident ID cannot be empty.", nameof(residentId));
        if (leaseId == Guid.Empty) throw new ArgumentException("Lease ID cannot be empty when specified.", nameof(leaseId));
        if (paymentIntentId == Guid.Empty) throw new ArgumentException("PaymentIntent ID cannot be empty when specified.", nameof(paymentIntentId));

        var roundedAmount = MoneyRounding.Round(amount);
        if (roundedAmount <= 0m)
        {
            throw new ArgumentException("Payment amount must be greater than zero.", nameof(amount));
        }

        if (paymentMethod == PaymentMethod.Cash && provider != PaymentProviderType.Manual)
        {
            throw new ArgumentException("Cash payments must use the Manual provider.", nameof(provider));
        }

        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            ResidentId = residentId,
            LeaseId = leaseId,
            PaymentIntentId = paymentIntentId,
            Amount = roundedAmount,
            Currency = CurrencyCode.Normalize(currency),
            ProviderFee = 0m,
            NetSettlementAmount = roundedAmount,
            AllocatedAmount = 0m,
            UnappliedAmount = roundedAmount,
            RefundedAmount = 0m,
            PaymentMethod = paymentMethod,
            Provider = provider,
            Status = PaymentStatus.Pending,
            OverpaymentPolicy = overpaymentPolicy,
            Reference = string.IsNullOrWhiteSpace(reference) ? null : reference.Trim(),
            IdempotencyKey = string.IsNullOrWhiteSpace(idempotencyKey) ? null : idempotencyKey.Trim(),
            MetadataJson = string.IsNullOrWhiteSpace(metadataJson) ? null : metadataJson.Trim(),
            RecordedByUserId = recordedByUserId,
            ReceivedAtUtc = receivedAtUtc,
            CreatedAtUtc = utcNow
        };

        payment._domainEvents.Add(new PaymentCreatedEvent(
            payment.Id,
            payment.OrganizationId,
            payment.ResidentId,
            payment.Amount,
            payment.Currency,
            payment.PaymentMethod,
            payment.Provider));

        return payment;
    }

    public void AssignPaymentNumber(string paymentNumber)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(paymentNumber, nameof(paymentNumber));
        if (!string.IsNullOrWhiteSpace(PaymentNumber))
        {
            throw new InvalidOperationException("Payment number has already been assigned.");
        }

        PaymentNumber = paymentNumber.Trim();
    }

    public void AttachExternalIdentifiers(
        string? providerPaymentId,
        string? providerCustomerId,
        string? providerTransactionId,
        string? providerStatus,
        DateTime utcNow)
    {
        if (!string.IsNullOrWhiteSpace(providerPaymentId)) ProviderPaymentId = providerPaymentId.Trim();
        if (!string.IsNullOrWhiteSpace(providerCustomerId)) ProviderCustomerId = providerCustomerId.Trim();
        if (!string.IsNullOrWhiteSpace(providerTransactionId)) ProviderTransactionId = providerTransactionId.Trim();
        if (!string.IsNullOrWhiteSpace(providerStatus)) ProviderStatus = providerStatus.Trim();
        UpdatedAtUtc = utcNow;
    }

    public void MarkProcessing(
        string? providerPaymentId,
        string? providerStatus,
        DateTime utcNow)
    {
        if (Status == PaymentStatus.Processing)
        {
            AttachExternalIdentifiers(providerPaymentId, null, null, providerStatus, utcNow);
            return;
        }

        if (Status != PaymentStatus.Pending)
        {
            throw new InvalidOperationException($"Cannot transition payment from '{Status}' to '{PaymentStatus.Processing}'.");
        }

        Status = PaymentStatus.Processing;
        AttachExternalIdentifiers(providerPaymentId, null, null, providerStatus, utcNow);
    }

    public void MarkSucceeded(
        DateTime utcNow,
        string? providerTransactionId = null,
        string? providerStatus = null,
        decimal? providerFee = null,
        DateTime? receivedAtUtc = null,
        Guid? confirmedByUserId = null)
    {
        if (Status == PaymentStatus.Succeeded)
        {
            return;
        }

        if (Status is not (PaymentStatus.Pending or PaymentStatus.Processing))
        {
            throw new InvalidOperationException($"Cannot transition payment from '{Status}' to '{PaymentStatus.Succeeded}'.");
        }

        if (providerFee.HasValue)
        {
            var roundedFee = MoneyRounding.Round(providerFee.Value);
            if (roundedFee < 0m || roundedFee > Amount)
            {
                throw new ArgumentException("Provider fee must be between 0 and the payment amount.", nameof(providerFee));
            }
            ProviderFee = roundedFee;
            NetSettlementAmount = MoneyRounding.Round(Amount - roundedFee);
        }

        if (!string.IsNullOrWhiteSpace(providerTransactionId))
        {
            ProviderTransactionId = providerTransactionId.Trim();
        }

        if (!string.IsNullOrWhiteSpace(providerStatus))
        {
            ProviderStatus = providerStatus.Trim();
        }

        Status = PaymentStatus.Succeeded;
        FailureReason = null;
        ReceivedAtUtc ??= receivedAtUtc ?? utcNow;
        if (confirmedByUserId.HasValue && confirmedByUserId.Value != Guid.Empty)
        {
            ConfirmedByUserId = confirmedByUserId;
            ConfirmedAtUtc = utcNow;
        }
        UpdatedAtUtc = utcNow;

        _domainEvents.Add(new PaymentSucceededEvent(
            Id,
            OrganizationId,
            ResidentId,
            Amount,
            Currency,
            Provider,
            ProviderTransactionId));
    }

    public void MarkFailed(string reason, DateTime utcNow, string? providerStatus = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason, nameof(reason));
        if (Status is not (PaymentStatus.Pending or PaymentStatus.Processing))
        {
            throw new InvalidOperationException($"Cannot transition payment from '{Status}' to '{PaymentStatus.Failed}'.");
        }

        Status = PaymentStatus.Failed;
        FailureReason = reason.Trim();
        if (!string.IsNullOrWhiteSpace(providerStatus))
        {
            ProviderStatus = providerStatus.Trim();
        }
        UpdatedAtUtc = utcNow;

        _domainEvents.Add(new PaymentFailedEvent(Id, OrganizationId, ResidentId, FailureReason));
    }

    public void Cancel(string reason, DateTime utcNow)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason, nameof(reason));
        if (Status is not (PaymentStatus.Pending or PaymentStatus.Processing))
        {
            throw new InvalidOperationException($"Cannot cancel payment in '{Status}' status.");
        }

        Status = PaymentStatus.Cancelled;
        FailureReason = reason.Trim();
        UpdatedAtUtc = utcNow;

        _domainEvents.Add(new PaymentCancelledEvent(Id, OrganizationId, ResidentId, FailureReason));
    }

    public void RecordSettlement(decimal providerFee, DateTime settledAtUtc, DateTime utcNow)
    {
        if (Status is not (PaymentStatus.Succeeded or PaymentStatus.PartiallyRefunded or PaymentStatus.Refunded))
        {
            throw new InvalidOperationException($"Cannot record settlement for payment in '{Status}' status.");
        }

        var roundedFee = MoneyRounding.Round(providerFee);
        if (roundedFee < 0m || roundedFee > Amount)
        {
            throw new ArgumentException("Provider fee must be between 0 and the payment amount.", nameof(providerFee));
        }

        ProviderFee = roundedFee;
        NetSettlementAmount = MoneyRounding.Round(Amount - roundedFee);
        SettledAtUtc = settledAtUtc;
        UpdatedAtUtc = utcNow;

        _domainEvents.Add(new PaymentSettledEvent(
            Id,
            OrganizationId,
            Amount,
            ProviderFee,
            NetSettlementAmount,
            Currency));
    }

    public PaymentAllocation AllocateToInvoice(
        Guid invoiceId,
        decimal amount,
        string invoiceCurrency,
        DateTime utcNow,
        Guid? allocatedByUserId = null)
    {
        if (Status is not (PaymentStatus.Succeeded or PaymentStatus.PartiallyRefunded))
        {
            throw new InvalidOperationException($"Cannot allocate payment in '{Status}' status.");
        }

        var normalizedInvoiceCurrency = CurrencyCode.Normalize(invoiceCurrency);
        if (!string.Equals(Currency, normalizedInvoiceCurrency, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Currency mismatch: payment is '{Currency}' while invoice is '{normalizedInvoiceCurrency}'.");
        }

        var roundedAmount = MoneyRounding.Round(amount);
        if (roundedAmount <= 0m)
        {
            throw new ArgumentException("Allocation amount must be greater than zero.", nameof(amount));
        }

        var availableToAllocate = MoneyRounding.Round(UnappliedAmount - PendingRefundAmount);
        if (roundedAmount > availableToAllocate)
        {
            throw new InvalidOperationException(
                $"Allocation amount ({roundedAmount:0.00}) exceeds available unapplied payment balance ({availableToAllocate:0.00}).");
        }

        var allocation = PaymentAllocation.Create(
            OrganizationId,
            Id,
            invoiceId,
            roundedAmount,
            Currency,
            utcNow,
            allocatedByUserId);

        _allocations.Add(allocation);
        RecalculateBalances();
        UpdatedAtUtc = utcNow;

        _domainEvents.Add(new PaymentAllocatedEvent(
            Id,
            allocation.Id,
            OrganizationId,
            invoiceId,
            allocation.Amount,
            Currency));

        return allocation;
    }

    public PaymentAllocation ReverseAllocation(
        Guid allocationId,
        string reason,
        DateTime utcNow,
        Guid? reversedByUserId = null)
    {
        var allocation = _allocations.FirstOrDefault(a => a.Id == allocationId)
            ?? throw new InvalidOperationException($"Allocation '{allocationId}' was not found on payment '{Id}'.");

        allocation.MarkReversed(reason, utcNow, reversedByUserId);
        RecalculateBalances();
        UpdatedAtUtc = utcNow;

        _domainEvents.Add(new PaymentAllocationReversedEvent(
            Id,
            allocation.Id,
            OrganizationId,
            allocation.InvoiceId,
            allocation.Amount,
            allocation.ReversalReason!));

        return allocation;
    }

    public Refund InitiateRefund(
        decimal amount,
        string reason,
        DateTime utcNow,
        Guid? requestedByUserId = null,
        string? idempotencyKey = null)
    {
        if (Status is not (PaymentStatus.Succeeded or PaymentStatus.PartiallyRefunded))
        {
            throw new InvalidOperationException($"Cannot refund payment in '{Status}' status.");
        }

        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            var existing = _refunds.FirstOrDefault(r =>
                string.Equals(r.IdempotencyKey, idempotencyKey.Trim(), StringComparison.Ordinal));
            if (existing is not null)
            {
                return existing;
            }
        }

        var roundedAmount = MoneyRounding.Round(amount);
        if (roundedAmount <= 0m)
        {
            throw new ArgumentException("Refund amount must be greater than zero.", nameof(amount));
        }

        if (roundedAmount > RemainingRefundableAmount)
        {
            throw new InvalidOperationException(
                $"Refund amount ({roundedAmount:0.00}) exceeds remaining refundable amount ({RemainingRefundableAmount:0.00}).");
        }

        if (roundedAmount > AvailableToRefundWithoutReversingAllocations)
        {
            throw new InvalidOperationException(
                $"Refund amount ({roundedAmount:0.00}) exceeds unapplied balance ({AvailableToRefundWithoutReversingAllocations:0.00}). Reverse invoice allocations before refunding allocated funds.");
        }

        var refund = Refund.Create(
            OrganizationId,
            Id,
            roundedAmount,
            Currency,
            reason,
            utcNow,
            requestedByUserId,
            idempotencyKey);

        _refunds.Add(refund);
        UpdatedAtUtc = utcNow;
        return refund;
    }

    public void MarkRefundProcessing(Guid refundId, string? providerRefundId, DateTime utcNow)
    {
        var refund = GetRefund(refundId);
        refund.MarkProcessing(providerRefundId);
        UpdatedAtUtc = utcNow;
    }

    public void CompleteRefund(Guid refundId, string? providerRefundId, DateTime utcNow)
    {
        var refund = GetRefund(refundId);
        if (refund.Status == RefundStatus.Succeeded)
        {
            return;
        }

        refund.MarkSucceeded(providerRefundId, utcNow);
        RecalculateBalances();

        Status = RefundedAmount == Amount
            ? PaymentStatus.Refunded
            : PaymentStatus.PartiallyRefunded;

        UpdatedAtUtc = utcNow;

        _domainEvents.Add(new PaymentRefundedEvent(
            Id,
            refund.Id,
            OrganizationId,
            ResidentId,
            refund.Amount,
            RefundedAmount,
            Currency,
            Status == PaymentStatus.Refunded));
    }

    public void FailRefund(Guid refundId, string failureReason, DateTime utcNow)
    {
        var refund = GetRefund(refundId);
        refund.MarkFailed(failureReason, utcNow);
        UpdatedAtUtc = utcNow;
    }

    public void RecordUnappliedCreditNotice()
    {
        if (UnappliedAmount > 0m)
        {
            _domainEvents.Add(new PaymentUnappliedCreditRecordedEvent(
                Id,
                OrganizationId,
                ResidentId,
                UnappliedAmount,
                Currency));
        }
    }

    private Refund GetRefund(Guid refundId) =>
        _refunds.FirstOrDefault(r => r.Id == refundId)
        ?? throw new InvalidOperationException($"Refund '{refundId}' was not found on payment '{Id}'.");

    private void RecalculateBalances()
    {
        AllocatedAmount = MoneyRounding.Round(_allocations.Where(a => !a.IsReversed).Sum(a => a.Amount));
        RefundedAmount = MoneyRounding.Round(_refunds.Where(r => r.Status == RefundStatus.Succeeded).Sum(r => r.Amount));
        UnappliedAmount = MoneyRounding.Round(Amount - AllocatedAmount - RefundedAmount);
    }
}
