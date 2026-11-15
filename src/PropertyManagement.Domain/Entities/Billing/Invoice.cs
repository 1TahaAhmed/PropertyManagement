using PropertyManagement.Domain.Common;
using PropertyManagement.Domain.Entities.Billing.Events;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Domain.Entities.Billing;

public sealed class Invoice : IOrganizationScoped, IHasDomainEvents
{
    private readonly List<InvoiceLine> _lines = [];
    private readonly List<InvoiceAdjustment> _adjustments = [];
    private readonly List<IDomainEvent> _domainEvents = [];

    private Invoice() { }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid LeaseId { get; private set; }
    public Guid PropertyId { get; private set; }
    public Guid UnitId { get; private set; }
    public Guid ResidentId { get; private set; }
    public string? InvoiceNumber { get; private set; }
    public string? IdempotencyKey { get; private set; }
    public DateOnly BillingPeriodStart { get; private set; }
    public DateOnly BillingPeriodEnd { get; private set; }
    public DateOnly IssueDate { get; private set; }
    public DateOnly DueDate { get; private set; }
    public string Currency { get; private set; } = CurrencyCode.Default;
    public InvoiceStatus Status { get; private set; }
    public decimal Subtotal { get; private set; }
    public decimal TotalAdjustments { get; private set; }
    public decimal Total { get; private set; }
    public decimal AmountPaid { get; private set; }
    public decimal BalanceDue { get; private set; }
    public string? Notes { get; private set; }
    public string? VoidReason { get; private set; }
    public DateTime? IssuedAtUtc { get; private set; }
    public DateTime? VoidedAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public BillingPeriod BillingPeriod => new(BillingPeriodStart, BillingPeriodEnd);
    public IReadOnlyCollection<InvoiceLine> Lines => _lines.AsReadOnly();
    public IReadOnlyCollection<InvoiceAdjustment> Adjustments => _adjustments.AsReadOnly();
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    public void ClearDomainEvents() => _domainEvents.Clear();

    public static Invoice CreateDraft(
        Guid organizationId,
        Guid leaseId,
        Guid propertyId,
        Guid unitId,
        Guid residentId,
        BillingPeriod billingPeriod,
        DateOnly issueDate,
        DateOnly dueDate,
        string currency,
        DateTime utcNow,
        string? notes = null,
        string? idempotencyKey = null)
    {
        if (organizationId == Guid.Empty) throw new ArgumentException("Organization ID cannot be empty.", nameof(organizationId));
        if (leaseId == Guid.Empty) throw new ArgumentException("Lease ID cannot be empty.", nameof(leaseId));
        if (propertyId == Guid.Empty) throw new ArgumentException("Property ID cannot be empty.", nameof(propertyId));
        if (unitId == Guid.Empty) throw new ArgumentException("Unit ID cannot be empty.", nameof(unitId));
        if (residentId == Guid.Empty) throw new ArgumentException("Resident ID cannot be empty.", nameof(residentId));
        if (dueDate < issueDate) throw new ArgumentException($"DueDate ({dueDate}) cannot be earlier than IssueDate ({issueDate}).");

        var normalizedCurrency = CurrencyCode.Normalize(currency);

        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            LeaseId = leaseId,
            PropertyId = propertyId,
            UnitId = unitId,
            ResidentId = residentId,
            BillingPeriodStart = billingPeriod.StartDate,
            BillingPeriodEnd = billingPeriod.EndDate,
            IssueDate = issueDate,
            DueDate = dueDate,
            Currency = normalizedCurrency,
            Status = InvoiceStatus.Draft,
            Subtotal = 0m,
            TotalAdjustments = 0m,
            Total = 0m,
            AmountPaid = 0m,
            BalanceDue = 0m,
            IdempotencyKey = string.IsNullOrWhiteSpace(idempotencyKey) ? null : idempotencyKey.Trim(),
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            CreatedAtUtc = utcNow
        };

        invoice._domainEvents.Add(new InvoiceCreatedEvent(invoice.Id, organizationId, leaseId, residentId));
        return invoice;
    }

    public void UpdateDraftMetadata(DateOnly issueDate, DateOnly dueDate, string? notes, DateTime utcNow)
    {
        EnsureInState(InvoiceStatus.Draft, "update metadata of");
        if (dueDate < issueDate)
        {
            throw new ArgumentException($"DueDate ({dueDate}) cannot be earlier than IssueDate ({issueDate}).");
        }

        IssueDate = issueDate;
        DueDate = dueDate;
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        UpdatedAtUtc = utcNow;
    }

    public InvoiceLine AddLine(
        ChargeType type,
        string description,
        decimal quantity,
        decimal unitPrice,
        DateTime utcNow,
        Guid? sourceChargeId = null,
        DateOnly? periodStart = null,
        DateOnly? periodEnd = null)
    {
        EnsureInState(InvoiceStatus.Draft, "add lines to");

        if (sourceChargeId.HasValue && sourceChargeId.Value != Guid.Empty && _lines.Any(l => l.SourceChargeId == sourceChargeId.Value))
        {
            throw new InvalidOperationException($"Charge '{sourceChargeId.Value}' is already included on this invoice.");
        }

        var line = InvoiceLine.Create(
            OrganizationId,
            Id,
            type,
            description,
            quantity,
            unitPrice,
            utcNow,
            sourceChargeId,
            periodStart,
            periodEnd);

        _lines.Add(line);
        RecalculateTotals();
        UpdatedAtUtc = utcNow;
        return line;
    }

    public void RemoveLine(Guid lineId, DateTime utcNow)
    {
        EnsureInState(InvoiceStatus.Draft, "remove lines from");

        var line = _lines.FirstOrDefault(l => l.Id == lineId)
            ?? throw new InvalidOperationException($"Invoice line '{lineId}' was not found on this invoice.");

        _lines.Remove(line);
        RecalculateTotals();
        UpdatedAtUtc = utcNow;
    }

    public void Issue(string invoiceNumber, DateOnly issueDate, DateTime utcNow)
    {
        EnsureInState(InvoiceStatus.Draft, "issue");
        ArgumentException.ThrowIfNullOrWhiteSpace(invoiceNumber, nameof(invoiceNumber));

        var trimmedNumber = invoiceNumber.Trim();
        if (trimmedNumber.Length > 50)
        {
            throw new ArgumentException("Invoice number cannot exceed 50 characters.", nameof(invoiceNumber));
        }

        if (_lines.Count == 0)
        {
            throw new InvalidOperationException("Cannot issue an invoice without at least one invoice line.");
        }

        RecalculateTotals();
        if (Subtotal < 0 || Total < 0)
        {
            throw new InvalidOperationException($"Cannot issue an invoice with negative total ({Total:0.00}). Discounts cannot exceed charges.");
        }

        if (DueDate < issueDate)
        {
            throw new InvalidOperationException($"Cannot issue invoice: DueDate ({DueDate}) cannot be earlier than IssueDate ({issueDate}).");
        }

        InvoiceNumber = trimmedNumber;
        IssueDate = issueDate;
        IssuedAtUtc = utcNow;
        Status = InvoiceStatus.Issued;
        UpdatedAtUtc = utcNow;

        _domainEvents.Add(new InvoiceIssuedEvent(
            Id,
            OrganizationId,
            LeaseId,
            ResidentId,
            trimmedNumber,
            Total,
            Currency,
            DueDate));
    }

    public InvoiceAdjustment ApplyAdjustment(
        InvoiceAdjustmentType type,
        ChargeType chargeType,
        decimal amount,
        string description,
        string reason,
        Guid actorUserId,
        DateTime utcNow)
    {
        if (Status is not (InvoiceStatus.Issued or InvoiceStatus.PartiallyPaid or InvoiceStatus.Overdue))
        {
            throw new InvalidOperationException(
                $"Cannot apply post-issue adjustment to invoice in '{Status}' status. Adjustments are only allowed on Issued, PartiallyPaid, or Overdue invoices.");
        }

        var adjustment = InvoiceAdjustment.Create(
            OrganizationId,
            Id,
            type,
            chargeType,
            amount,
            description,
            reason,
            actorUserId,
            utcNow);

        var candidateTotalAdjustments = MoneyRounding.Round(TotalAdjustments + adjustment.SignedAmount);
        var candidateTotal = MoneyRounding.Round(Subtotal + candidateTotalAdjustments);

        if (candidateTotal < 0)
        {
            throw new InvalidOperationException(
                $"Credit adjustment of {adjustment.Amount:0.00} would reduce invoice total below zero ({candidateTotal:0.00}).");
        }

        if (candidateTotal < AmountPaid)
        {
            throw new InvalidOperationException(
                $"Credit adjustment of {adjustment.Amount:0.00} would reduce invoice total ({candidateTotal:0.00}) below AmountPaid ({AmountPaid:0.00}). Process a refund instead.");
        }

        _adjustments.Add(adjustment);
        RecalculateTotals();

        if (Status is InvoiceStatus.Issued or InvoiceStatus.PartiallyPaid or InvoiceStatus.Overdue && BalanceDue == 0m)
        {
            Status = InvoiceStatus.Paid;
        }

        UpdatedAtUtc = utcNow;

        _domainEvents.Add(new InvoiceAdjustedEvent(
            Id,
            adjustment.Id,
            OrganizationId,
            adjustment.Type,
            adjustment.ChargeType,
            adjustment.Amount,
            Total,
            BalanceDue));

        return adjustment;
    }

    public void MarkOverdue(DateOnly asOfDate, DateTime utcNow)
    {
        if (Status is not (InvoiceStatus.Issued or InvoiceStatus.PartiallyPaid))
        {
            throw new InvalidOperationException($"Cannot mark invoice in '{Status}' status as Overdue.");
        }

        if (asOfDate <= DueDate)
        {
            throw new InvalidOperationException($"Invoice is not overdue as of {asOfDate}; DueDate is {DueDate}.");
        }

        if (BalanceDue <= 0m)
        {
            throw new InvalidOperationException("Cannot mark an invoice with zero balance due as Overdue.");
        }

        Status = InvoiceStatus.Overdue;
        UpdatedAtUtc = utcNow;
        _domainEvents.Add(new InvoiceMarkedOverdueEvent(Id, OrganizationId, DueDate));
    }

    public void RecordPaymentAllocation(decimal amount, DateTime utcNow)
    {
        if (Status is not (InvoiceStatus.Issued or InvoiceStatus.PartiallyPaid or InvoiceStatus.Overdue))
        {
            throw new InvalidOperationException($"Cannot allocate payment to invoice in '{Status}' status.");
        }

        var roundedAmount = MoneyRounding.Round(amount);
        if (roundedAmount <= 0m)
        {
            throw new ArgumentException("Payment allocation amount must be greater than zero.", nameof(amount));
        }

        if (roundedAmount > BalanceDue)
        {
            throw new InvalidOperationException($"Payment allocation ({roundedAmount:0.00}) exceeds remaining BalanceDue ({BalanceDue:0.00}).");
        }

        AmountPaid = MoneyRounding.Round(AmountPaid + roundedAmount);
        RecalculateTotals();
        Status = BalanceDue == 0m ? InvoiceStatus.Paid : InvoiceStatus.PartiallyPaid;
        UpdatedAtUtc = utcNow;
    }

    public void Void(string reason, DateTime utcNow)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason, nameof(reason));

        if (Status is InvoiceStatus.Void or InvoiceStatus.Paid or InvoiceStatus.PartiallyPaid)
        {
            throw new InvalidOperationException($"Cannot void an invoice in '{Status}' status.");
        }

        if (AmountPaid > 0m)
        {
            throw new InvalidOperationException("Cannot void an invoice that has recorded payments.");
        }

        Status = InvoiceStatus.Void;
        VoidReason = reason.Trim();
        VoidedAtUtc = utcNow;
        BalanceDue = 0m;
        UpdatedAtUtc = utcNow;

        _domainEvents.Add(new InvoiceVoidedEvent(Id, OrganizationId, LeaseId, InvoiceNumber, VoidReason));
    }

    private void RecalculateTotals()
    {
        Subtotal = MoneyRounding.Round(_lines.Sum(l => l.Amount));
        TotalAdjustments = MoneyRounding.Round(_adjustments.Sum(a => a.SignedAmount));
        Total = MoneyRounding.Round(Subtotal + TotalAdjustments);
        BalanceDue = Status == InvoiceStatus.Void
            ? 0m
            : MoneyRounding.Round(Total - AmountPaid);
    }

    private void EnsureInState(InvoiceStatus expectedStatus, string operation)
    {
        if (Status != expectedStatus)
        {
            throw new InvalidOperationException($"Cannot {operation} invoice in '{Status}' status. Expected status: '{expectedStatus}'.");
        }
    }
}
