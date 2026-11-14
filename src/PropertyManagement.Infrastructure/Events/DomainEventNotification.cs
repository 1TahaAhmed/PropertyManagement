using MediatR;
using PropertyManagement.Domain.Common;

namespace PropertyManagement.Infrastructure.Events;

public sealed record DomainEventNotification<TDomainEvent>(TDomainEvent DomainEvent) : INotification
    where TDomainEvent : IDomainEvent;
