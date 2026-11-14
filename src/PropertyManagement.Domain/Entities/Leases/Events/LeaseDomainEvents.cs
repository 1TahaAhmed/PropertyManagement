using PropertyManagement.Domain.Common;

namespace PropertyManagement.Domain.Entities.Leases.Events;

public sealed record LeaseCreatedEvent(Guid LeaseId, Guid OrganizationId, Guid PropertyId, Guid UnitId) : IDomainEvent;
public sealed record LeaseSubmittedEvent(Guid LeaseId, Guid OrganizationId) : IDomainEvent;
public sealed record LeaseApprovedEvent(Guid LeaseId, Guid OrganizationId, Guid UnitId) : IDomainEvent;
public sealed record LeaseTerminatedEvent(Guid LeaseId, Guid OrganizationId, Guid UnitId, DateOnly TerminationDate, string Reason) : IDomainEvent;
public sealed record LeaseCancelledEvent(Guid LeaseId, Guid OrganizationId, Guid UnitId) : IDomainEvent;
public sealed record LeaseRenewedEvent(Guid OldLeaseId, Guid NewLeaseId, Guid OrganizationId) : IDomainEvent;
