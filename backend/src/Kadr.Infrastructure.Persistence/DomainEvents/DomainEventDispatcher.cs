using Kadr.SharedKernel.Application;
using Kadr.SharedKernel.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace Kadr.Infrastructure.Persistence.DomainEvents;

public interface IDomainEventDispatcher
{
    Task DispatchAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken cancellationToken = default);
}

public sealed class DomainEventDispatcher(IServiceProvider serviceProvider) : IDomainEventDispatcher
{
    public async Task DispatchAsync(
        IEnumerable<IDomainEvent> domainEvents,
        CancellationToken cancellationToken = default)
    {
        foreach (var domainEvent in domainEvents)
        {
            var handlerType = typeof(IDomainEventHandler<>).MakeGenericType(domainEvent.GetType());
            var handlers = serviceProvider.GetServices(handlerType);

            foreach (var handler in handlers)
            {
                var method = handlerType.GetMethod(nameof(IDomainEventHandler<IDomainEvent>.HandleAsync))
                    ?? throw new InvalidOperationException($"HandleAsync not found on {handlerType.Name}.");

                var task = (Task?)method.Invoke(handler, [domainEvent, cancellationToken])
                    ?? throw new InvalidOperationException("Domain event handler returned null.");

                await task.ConfigureAwait(false);
            }
        }
    }
}
