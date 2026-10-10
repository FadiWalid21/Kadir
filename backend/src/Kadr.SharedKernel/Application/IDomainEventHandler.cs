using Kadr.SharedKernel.Domain;

namespace Kadr.SharedKernel.Application;

public interface IDomainEventHandler<in TEvent>
    where TEvent : IDomainEvent
{
    Task HandleAsync(TEvent domainEvent, CancellationToken cancellationToken = default);
}
