using MediatR;

namespace Contracts.Common.Messages;
public interface IDomainEventHandler<TEvent> : INotificationHandler<TEvent>
    where TEvent : IDomainEvent
{
}
