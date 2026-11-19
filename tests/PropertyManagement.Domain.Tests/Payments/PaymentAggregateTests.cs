using PropertyManagement.Domain.Entities.Payments;
using PropertyManagement.Domain.Entities.Payments.Events;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Domain.Tests.Payments;

public sealed class PaymentAggregateTests
{
    private static readonly DateTime Now = new(2026, 4, 1, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void StateMachine_ShouldAllowValidTransitions_AndRejectInvalidOnes()
    {
        var payment = Payment.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            10000m,
            "egp",
            PaymentMethod.Card,
            PaymentProviderType.Stripe,
            Now);

        Assert.Equal(PaymentStatus.Pending, payment.Status);
        Assert.Equal(10000m, payment.UnappliedAmount);

        payment.MarkProcessing("pay_ext_1", "processing", Now.AddSeconds(5));
        Assert.Equal(PaymentStatus.Processing, payment.Status);

        payment.MarkSucceeded(Now.AddSeconds(30), providerTransactionId: "tx_ext_1", providerStatus: "succeeded");
        Assert.Equal(PaymentStatus.Succeeded, payment.Status);
        Assert.Contains(payment.DomainEvents, e => e is PaymentSucceededEvent);

        // Cannot cancel or fail after succeeded
        Assert.Throws<InvalidOperationException>(() => payment.Cancel("Too late", Now.AddMinutes(1)));
        Assert.Throws<InvalidOperationException>(() => payment.MarkFailed("Too late", Now.AddMinutes(1)));

        // Failed payment cannot transition to Succeeded or Refunded
        var failedPayment = Payment.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            2000m,
            "EGP",
            PaymentMethod.BankTransfer,
            PaymentProviderType.Bank,
            Now);
        failedPayment.MarkFailed("Insufficient funds", Now.AddMinutes(1));
        Assert.Throws<InvalidOperationException>(() => failedPayment.MarkSucceeded(Now.AddMinutes(2)));
        Assert.Throws<InvalidOperationException>(() => failedPayment.InitiateRefund(500m, "Invalid", Now.AddMinutes(2)));

        // Cancelled payment cannot transition to Succeeded
        var cancelled = Payment.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            2000m,
            "EGP",
            PaymentMethod.Card,
            PaymentProviderType.Stripe,
            Now);
        cancelled.Cancel("User aborted", Now.AddMinutes(1));
        Assert.Throws<InvalidOperationException>(() => cancelled.MarkSucceeded(Now.AddMinutes(2)));
    }

    [Fact]
    public void CashPayment_MustUseManualProvider()
    {
        Assert.Throws<ArgumentException>(() => Payment.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            1000m,
            "EGP",
            PaymentMethod.Cash,
            PaymentProviderType.Stripe,
            Now));
    }

    [Fact]
    public void Allocations_PartialAndMultiInvoice_AndReversalWithoutDeletingHistory()
    {
        var payment = Payment.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            12000m,
            "EGP",
            PaymentMethod.BankTransfer,
            PaymentProviderType.Manual,
            Now);

        payment.MarkSucceeded(Now.AddMinutes(1), confirmedByUserId: Guid.NewGuid());

        var invA = Guid.NewGuid();
        var invB = Guid.NewGuid();

        var allocA = payment.AllocateToInvoice(invA, 5000m, "EGP", Now.AddMinutes(2));
        var allocB = payment.AllocateToInvoice(invB, 5000m, "EGP", Now.AddMinutes(2));

        Assert.Equal(10000m, payment.AllocatedAmount);
        Assert.Equal(2000m, payment.UnappliedAmount);
        Assert.Equal(2, payment.Allocations.Count);

        // Exceeds remaining unapplied (2000)
        Assert.Throws<InvalidOperationException>(() => payment.AllocateToInvoice(Guid.NewGuid(), 2500m, "EGP", Now.AddMinutes(3)));

        // Currency mismatch
        Assert.Throws<InvalidOperationException>(() => payment.AllocateToInvoice(Guid.NewGuid(), 500m, "USD", Now.AddMinutes(3)));

        // Reverse allocation preserves history instead of deleting
        payment.ReverseAllocation(allocA.Id, "Applied to wrong invoice", Now.AddMinutes(4), Guid.NewGuid());
        Assert.Equal(2, payment.Allocations.Count);
        Assert.True(allocA.IsReversed);
        Assert.False(allocB.IsReversed);
        Assert.Equal(5000m, payment.AllocatedAmount);
        Assert.Equal(7000m, payment.UnappliedAmount);
        Assert.Contains(payment.DomainEvents, e => e is PaymentAllocationReversedEvent);
    }

    [Fact]
    public void Refunds_PartialAndFull_ShouldEnforceLimitsAndPreserveHistory()
    {
        var payment = Payment.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            10000m,
            "EGP",
            PaymentMethod.Card,
            PaymentProviderType.Stripe,
            Now);

        payment.MarkSucceeded(Now.AddMinutes(1), providerTransactionId: "tx_1");
        var alloc = payment.AllocateToInvoice(Guid.NewGuid(), 4000m, "EGP", Now.AddMinutes(2));

        // Cannot refund allocated money without reversing allocation first
        Assert.Throws<InvalidOperationException>(() => payment.InitiateRefund(7000m, "Over unapplied", Now.AddMinutes(3)));

        // Partial refund on unapplied portion (6000 available)
        var refund1 = payment.InitiateRefund(3000m, "Overpayment return", Now.AddMinutes(4), Guid.NewGuid(), "ref-key-1");
        // Idempotent retry with same key returns same refund
        var refund1Dup = payment.InitiateRefund(3000m, "Overpayment return", Now.AddMinutes(4), Guid.NewGuid(), "ref-key-1");
        Assert.Equal(refund1.Id, refund1Dup.Id);

        payment.MarkRefundProcessing(refund1.Id, "re_1", Now.AddMinutes(5));
        payment.CompleteRefund(refund1.Id, "re_1", Now.AddMinutes(6));

        Assert.Equal(PaymentStatus.PartiallyRefunded, payment.Status);
        Assert.Equal(3000m, payment.RefundedAmount);
        Assert.Equal(7000m, payment.RemainingRefundableAmount);
        Assert.Equal(3000m, payment.UnappliedAmount);

        // Reverse remaining 4000 allocation, then refund remaining 7000
        payment.ReverseAllocation(alloc.Id, "Full refund requested", Now.AddMinutes(7));
        Assert.Equal(7000m, payment.UnappliedAmount);

        var refund2 = payment.InitiateRefund(7000m, "Final refund", Now.AddMinutes(8), Guid.NewGuid(), "ref-key-2");
        payment.CompleteRefund(refund2.Id, "re_2", Now.AddMinutes(9));

        Assert.Equal(PaymentStatus.Refunded, payment.Status);
        Assert.Equal(10000m, payment.RefundedAmount);
        Assert.Equal(0m, payment.RemainingRefundableAmount);
        Assert.Equal(0m, payment.UnappliedAmount);
        Assert.Equal(2, payment.Refunds.Count);
        Assert.Throws<InvalidOperationException>(() => payment.InitiateRefund(1m, "Exhausted", Now.AddMinutes(10)));
    }

    [Fact]
    public void RecordSettlement_ShouldTrackGrossFeeAndNetWithoutMutatingGrossAmount()
    {
        var payment = Payment.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            10000m,
            "EGP",
            PaymentMethod.Card,
            PaymentProviderType.Stripe,
            Now);

        payment.MarkSucceeded(Now.AddMinutes(1), providerTransactionId: "tx_100");
        payment.RecordSettlement(250m, Now.AddDays(1), Now.AddDays(1));

        Assert.Equal(10000m, payment.Amount);
        Assert.Equal(250m, payment.ProviderFee);
        Assert.Equal(9750m, payment.NetSettlementAmount);
        Assert.Contains(payment.DomainEvents, e => e is PaymentSettledEvent settled && settled.NetSettlementAmount == 9750m);
    }
}
