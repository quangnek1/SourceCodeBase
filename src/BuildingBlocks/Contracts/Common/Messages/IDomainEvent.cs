using MediatR;

namespace Contracts.Common.Messages;
public interface IDomainEvent : INotification
{
    Guid Id { get; init; }
}
