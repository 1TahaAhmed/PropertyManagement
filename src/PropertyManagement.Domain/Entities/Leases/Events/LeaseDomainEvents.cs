namespace PropertyManagement.Domain.Entities.Leases.Events;

public sealed record LeaseCreatedEvent(Guid LeaseId, Guid OrganizationId, Guid PropertyId, Guid UnitId);
public sealed record LeaseSubmittedEvent(Guid LeaseId, Guid OrganizationId);
public sealed record LeaseApprovedEvent(Guid LeaseId, Guid OrganizationId, Guid UnitId);
public sealed record LeaseTerminatedEvent(Guid LeaseId, Guid OrganizationId, Guid UnitId, DateOnly TerminationDate, string Reason);
public sealed record LeaseCancelledEvent(Guid LeaseId, Guid OrganizationId, Guid UnitId);
public sealed record LeaseRenewedEvent(Guid OldLeaseId, Guid NewLeaseId, Guid OrganizationId);
