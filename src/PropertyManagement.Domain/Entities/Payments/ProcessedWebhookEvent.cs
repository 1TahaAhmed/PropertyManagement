using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Domain.Entities.Payments;

public sealed class ProcessedWebhookEvent
{
    private ProcessedWebhookEvent() { }

    public Guid Id { get; private set; }
    public Guid? OrganizationId { get; private set; }
    public PaymentProviderType Provider { get; private set; }
    public string ProviderEventId { get; private set; } = string.Empty;
    public string EventType { get; private set; } = string.Empty;
    public string PayloadHash { get; private set; } = string.Empty;
    public WebhookEventStatus Status { get; private set; }
    public DateTime EventTimestampUtc { get; private set; }
    public DateTime ReceivedAtUtc { get; private set; }
    public DateTime? ProcessedAtUtc { get; private set; }
    public string? FailureReason { get; private set; }

    public static ProcessedWebhookEvent CreateReceived(
        PaymentProviderType provider,
        string providerEventId,
        string eventType,
        string payloadHash,
        DateTime eventTimestampUtc,
        DateTime utcNow,
        Guid? organizationId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerEventId, nameof(providerEventId));
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType, nameof(eventType));
        ArgumentException.ThrowIfNullOrWhiteSpace(payloadHash, nameof(payloadHash));

        return new ProcessedWebhookEvent
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Provider = provider,
            ProviderEventId = providerEventId.Trim(),
            EventType = eventType.Trim(),
            PayloadHash = payloadHash.Trim(),
            Status = WebhookEventStatus.Received,
            EventTimestampUtc = eventTimestampUtc,
            ReceivedAtUtc = utcNow
        };
    }

    public void MarkProcessed(DateTime utcNow, Guid? organizationId = null)
    {
        if (organizationId.HasValue && organizationId.Value != Guid.Empty)
        {
            OrganizationId = organizationId;
        }

        Status = WebhookEventStatus.Processed;
        FailureReason = null;
        ProcessedAtUtc = utcNow;
    }

    public void MarkIgnoredDuplicate(DateTime utcNow)
    {
        Status = WebhookEventStatus.IgnoredDuplicate;
        ProcessedAtUtc = utcNow;
    }

    public void MarkFailed(string reason, DateTime utcNow, Guid? organizationId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason, nameof(reason));
        if (organizationId.HasValue && organizationId.Value != Guid.Empty)
        {
            OrganizationId = organizationId;
        }

        Status = WebhookEventStatus.Failed;
        FailureReason = reason.Trim();
        ProcessedAtUtc = utcNow;
    }
}
