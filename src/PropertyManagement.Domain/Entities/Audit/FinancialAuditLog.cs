using PropertyManagement.Domain.Common;

namespace PropertyManagement.Domain.Entities.Audit;

public sealed class FinancialAuditLog : IOrganizationScoped
{
    private FinancialAuditLog() { }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid? ActorUserId { get; private set; }
    public string Action { get; private set; } = string.Empty;
    public string EntityType { get; private set; } = string.Empty;
    public Guid EntityId { get; private set; }
    public string DetailsJson { get; private set; } = "{}";
    public string? CorrelationId { get; private set; }
    public DateTime OccurredAtUtc { get; private set; }

    public static FinancialAuditLog Record(
        Guid organizationId,
        string action,
        string entityType,
        Guid entityId,
        string detailsJson,
        DateTime utcNow,
        Guid? actorUserId = null,
        string? correlationId = null)
    {
        if (organizationId == Guid.Empty) throw new ArgumentException("Organization ID cannot be empty.", nameof(organizationId));
        if (entityId == Guid.Empty) throw new ArgumentException("Entity ID cannot be empty.", nameof(entityId));
        ArgumentException.ThrowIfNullOrWhiteSpace(action, nameof(action));
        ArgumentException.ThrowIfNullOrWhiteSpace(entityType, nameof(entityType));

        return new FinancialAuditLog
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            ActorUserId = actorUserId,
            Action = action.Trim(),
            EntityType = entityType.Trim(),
            EntityId = entityId,
            DetailsJson = string.IsNullOrWhiteSpace(detailsJson) ? "{}" : detailsJson.Trim(),
            CorrelationId = string.IsNullOrWhiteSpace(correlationId) ? null : correlationId.Trim(),
            OccurredAtUtc = utcNow
        };
    }
}
