using PropertyManagement.Domain.Entities.Billing;
using PropertyManagement.Domain.Entities.Billing.Events;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Domain.Tests.Billing;

public sealed class InvoiceAggregateTests
{
    private static readonly DateTime FixedNow = new(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc);
    private static readonly BillingPeriod March2026 = BillingPeriod.ForMonth(2026, 3);

    private static Invoice CreateSampleDraftInvoice(decimal rent = 10000m, decimal discount = 0m)
    {
        var invoice = Invoice.CreateDraft(
            organizationId: Guid.NewGuid(),
            leaseId: Guid.NewGuid(),
            propertyId: Guid.NewGuid(),
            unitId: Guid.NewGuid(),
            residentId: Guid.NewGuid(),
            billingPeriod: March2026,
            issueDate: new DateOnly(2026, 3, 1),
            dueDate: new DateOnly(2026, 3, 5),
            currency: "egp",
            utcNow: FixedNow);

        invoice.AddLine(ChargeType.Rent, "March 2026 Rent", 1m, rent, FixedNow);
        if (discount > 0m)
        {
            invoice.AddLine(ChargeType.Discount, "Loyalty Discount", 1m, discount, FixedNow);
        }

        return invoice;
    }

    [Fact]
    public void CreateDraft_AddLines_AndIssue_ShouldCalculateTotalsAndEmitEvents()
    {
        var invoice = CreateSampleDraftInvoice(rent: 10000m, discount: 1000m);

        Assert.Equal("EGP", invoice.Currency);
        Assert.Equal(InvoiceStatus.Draft, invoice.Status);
        Assert.Equal(9000.00m, invoice.Subtotal);
        Assert.Equal(9000.00m, invoice.Total);
        Assert.Equal(9000.00m, invoice.BalanceDue);

        invoice.Issue("INV-2026-000001", new DateOnly(2026, 3, 1), FixedNow.AddMinutes(10));

        Assert.Equal(InvoiceStatus.Issued, invoice.Status);
        Assert.Equal("INV-2026-000001", invoice.InvoiceNumber);
        Assert.Contains(invoice.DomainEvents, e => e is InvoiceCreatedEvent);
        Assert.Contains(invoice.DomainEvents, e => e is InvoiceIssuedEvent issued && issued.Total == 9000.00m);
    }

    [Fact]
    public void IssuedInvoice_ShouldLockLines_AndRequirePostIssueAdjustments()
    {
        var invoice = CreateSampleDraftInvoice(rent: 10000m);
        invoice.Issue("INV-2026-000002", new DateOnly(2026, 3, 1), FixedNow);

        Assert.Throws<InvalidOperationException>(() =>
            invoice.AddLine(ChargeType.Parking, "Parking", 1m, 500m, FixedNow.AddDays(1)));
        Assert.Throws<InvalidOperationException>(() =>
            invoice.RemoveLine(invoice.Lines.First().Id, FixedNow.AddDays(1)));
        Assert.Throws<InvalidOperationException>(() =>
            invoice.UpdateDraftMetadata(new DateOnly(2026, 3, 2), new DateOnly(2026, 3, 10), "Edit", FixedNow.AddDays(1)));

        var actorId = Guid.NewGuid();
        invoice.ApplyAdjustment(
            InvoiceAdjustmentType.Debit,
            ChargeType.LateFee,
            500m,
            "Late payment fee",
            "Overdue past 5-day grace period",
            actorId,
            FixedNow.AddDays(11));

        invoice.ApplyAdjustment(
            InvoiceAdjustmentType.Credit,
            ChargeType.CreditAdjustment,
            300m,
            "Goodwill maintenance credit",
            "AC outage compensation",
            actorId,
            FixedNow.AddDays(12));

        Assert.Single(invoice.Lines);
        Assert.Equal(10000m, invoice.Subtotal);
        Assert.Equal(200m, invoice.TotalAdjustments);
        Assert.Equal(10200m, invoice.Total);
        Assert.Equal(10200m, invoice.BalanceDue);
        Assert.Equal(2, invoice.Adjustments.Count);
    }

    [Fact]
    public void Issue_WhenEmptyOrNegativeTotal_ShouldThrow()
    {
        var emptyInvoice = Invoice.CreateDraft(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            March2026, new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 5), "USD", FixedNow);

        Assert.Throws<InvalidOperationException>(() =>
            emptyInvoice.Issue("INV-2026-000003", new DateOnly(2026, 3, 1), FixedNow));

        var negativeInvoice = CreateSampleDraftInvoice(rent: 1000m, discount: 1500m);
        Assert.Throws<InvalidOperationException>(() =>
            negativeInvoice.Issue("INV-2026-000004", new DateOnly(2026, 3, 1), FixedNow));
    }

    [Fact]
    public void ApplyAdjustment_WhenCreditExceedsRemainingPayableAmount_ShouldThrow()
    {
        var invoice = CreateSampleDraftInvoice(rent: 10000m);
        invoice.Issue("INV-2026-000005", new DateOnly(2026, 3, 1), FixedNow);
        invoice.RecordPaymentAllocation(7000m, FixedNow.AddDays(2));

        Assert.Equal(InvoiceStatus.PartiallyPaid, invoice.Status);
        Assert.Equal(3000m, invoice.BalanceDue);

        Assert.Throws<InvalidOperationException>(() =>
            invoice.ApplyAdjustment(
                InvoiceAdjustmentType.Credit,
                ChargeType.CreditAdjustment,
                3500m,
                "Over-credit",
                "Invalid credit",
                Guid.NewGuid(),
                FixedNow.AddDays(3)));

        invoice.ApplyAdjustment(
            InvoiceAdjustmentType.Credit,
            ChargeType.CreditAdjustment,
            3000m,
            "Final balance waiver",
            "Approved settlement",
            Guid.NewGuid(),
            FixedNow.AddDays(3));

        Assert.Equal(0m, invoice.BalanceDue);
        Assert.Equal(InvoiceStatus.Paid, invoice.Status);
    }

    [Fact]
    public void StateMachine_Overdue_Payment_AndVoidGuards_ShouldEnforceTransitions()
    {
        var invoice = CreateSampleDraftInvoice(rent: 10000m);
        invoice.Issue("INV-2026-000006", new DateOnly(2026, 3, 1), FixedNow);

        Assert.Throws<InvalidOperationException>(() =>
            invoice.MarkOverdue(new DateOnly(2026, 3, 5), FixedNow.AddDays(4)));

        invoice.MarkOverdue(new DateOnly(2026, 3, 6), FixedNow.AddDays(5));
        Assert.Equal(InvoiceStatus.Overdue, invoice.Status);
        Assert.Contains(invoice.DomainEvents, e => e is InvoiceMarkedOverdueEvent);

        invoice.Void("Duplicate invoice generated", FixedNow.AddDays(6));
        Assert.Equal(InvoiceStatus.Void, invoice.Status);
        Assert.Equal(10000m, invoice.Total);
        Assert.Equal(0m, invoice.BalanceDue);
        Assert.Contains(invoice.DomainEvents, e => e is InvoiceVoidedEvent);

        Assert.Throws<InvalidOperationException>(() => invoice.Void("Again", FixedNow.AddDays(7)));
        Assert.Throws<InvalidOperationException>(() => invoice.RecordPaymentAllocation(100m, FixedNow.AddDays(7)));
    }

    [Fact]
    public void Void_WhenPartiallyPaidOrPaid_ShouldThrow()
    {
        var invoice = CreateSampleDraftInvoice(rent: 5000m);
        invoice.Issue("INV-2026-000007", new DateOnly(2026, 3, 1), FixedNow);
        invoice.RecordPaymentAllocation(5000m, FixedNow.AddDays(1));

        Assert.Equal(InvoiceStatus.Paid, invoice.Status);
        Assert.Throws<InvalidOperationException>(() => invoice.Void("Cannot void paid", FixedNow.AddDays(2)));
    }

    [Fact]
    public void ReversePaymentAllocation_ShouldRestorePartialOrOverdueOrIssuedStatus()
    {
        var invoice = CreateSampleDraftInvoice(rent: 5000m);
        invoice.Issue("INV-2026-000008", new DateOnly(2026, 3, 1), FixedNow);
        invoice.RecordPaymentAllocation(5000m, FixedNow.AddDays(1));
        Assert.Equal(InvoiceStatus.Paid, invoice.Status);

        // Partial reversal -> PartiallyPaid
        invoice.ReversePaymentAllocation(2000m, new DateOnly(2026, 3, 3), FixedNow.AddDays(2));
        Assert.Equal(3000m, invoice.AmountPaid);
        Assert.Equal(2000m, invoice.BalanceDue);
        Assert.Equal(InvoiceStatus.PartiallyPaid, invoice.Status);

        // Full reversal after due date -> Overdue
        invoice.ReversePaymentAllocation(3000m, new DateOnly(2026, 3, 10), FixedNow.AddDays(9));
        Assert.Equal(0m, invoice.AmountPaid);
        Assert.Equal(5000m, invoice.BalanceDue);
        Assert.Equal(InvoiceStatus.Overdue, invoice.Status);
    }
}
