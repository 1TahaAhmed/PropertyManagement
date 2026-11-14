using MediatR;
using PropertyManagement.Application.Abstractions.Events;
using PropertyManagement.Domain.Common;

namespace PropertyManagement.Infrastructure.Events;

public sealed class MediatRDomainEventDispatcher(IPublisher publisher) : IDomainEventDispatcher
{
    public async Task DispatchAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken cancellationToken = default)
    {
        foreach (var domainEvent in domainEvents)
        {
            var notificationType = typeof(DomainEventNotification<>).MakeGenericType(domainEvent.GetType());
            var notification = Activator.CreateInstance(notificationType, domainEvent);
            if (notification is INotification mediatrNotification)
            {
                await publisher.Publish(mediatrNotification, cancellationToken);
            }
        }
    }
}
