using PropertyManagement.Domain.Entities.Payments;
using PropertyManagement.Domain.Entities.Payments.Events;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Domain.Tests.Payments;

public sealed class PaymentIntentAggregateTests
{
    private static readonly DateTime Now = new(2026, 4, 1, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Create_WithValidData_ShouldInitializePendingAndEmitEvent()
    {
        var orgId = Guid.NewGuid();
        var residentId = Guid.NewGuid();

        var intent = PaymentIntent.Create(
            orgId,
            residentId,
            10000.555m,
            "egp",
            PaymentMethod.Card,
            PaymentProviderType.Stripe,
            Now.AddMinutes(30),
            Now,
            idempotencyKey: "pi-key-1");

        Assert.Equal(10000.56m, intent.Amount);
        Assert.Equal("EGP", intent.Currency);
        Assert.Equal(PaymentIntentStatus.Pending, intent.Status);
        Assert.Contains(intent.DomainEvents, e => e is PaymentIntentCreatedEvent);
    }

    [Fact]
    public void Lifecycle_AttachSession_MarkSucceeded_AndForbidInvalidTransitions()
    {
        var intent = PaymentIntent.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            5000m,
            "USD",
            PaymentMethod.Card,
            PaymentProviderType.Stripe,
            Now.AddHours(1),
            Now);

        intent.AttachProviderSession("pi_123", "cs_secret", "https://pay.test/action", requiresAction: true, Now.AddMinutes(1));
        Assert.Equal(PaymentIntentStatus.RequiresAction, intent.Status);
        Assert.Equal("pi_123", intent.ProviderIntentId);

        intent.MarkProcessing(Now.AddMinutes(2));
        Assert.Equal(PaymentIntentStatus.Processing, intent.Status);

        intent.MarkSucceeded(Now.AddMinutes(3));
        Assert.Equal(PaymentIntentStatus.Succeeded, intent.Status);

        Assert.Throws<InvalidOperationException>(() => intent.Cancel("Too late", Now.AddMinutes(4)));
        Assert.Throws<InvalidOperationException>(() => intent.MarkFailed("Too late", Now.AddMinutes(4)));
    }

    [Fact]
    public void ExpireIfPastDue_ShouldExpirePendingOnlyAfterDeadline()
    {
        var intent = PaymentIntent.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            2000m,
            "EGP",
            PaymentMethod.Card,
            PaymentProviderType.Paymob,
            Now.AddMinutes(15),
            Now);

        Assert.Throws<InvalidOperationException>(() => intent.ExpireIfPastDue(Now.AddMinutes(10)));

        intent.ExpireIfPastDue(Now.AddMinutes(15));
        Assert.Equal(PaymentIntentStatus.Expired, intent.Status);
        Assert.Throws<InvalidOperationException>(() => intent.MarkSucceeded(Now.AddMinutes(20)));
    }
}
