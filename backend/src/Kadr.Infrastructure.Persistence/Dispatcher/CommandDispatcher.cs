using Kadr.SharedKernel.Application;
using Kadr.SharedKernel.Results;
using Microsoft.Extensions.DependencyInjection;

namespace Kadr.Infrastructure.Persistence.Dispatcher;

public sealed class CommandDispatcher(IServiceProvider serviceProvider) : ICommandDispatcher
{
    public async Task<Result> SendAsync(ICommand command, CancellationToken cancellationToken = default)
    {
        var handlerType = typeof(ICommandHandler<>).MakeGenericType(command.GetType());
        var handler = serviceProvider.GetRequiredService(handlerType);
        var method = handlerType.GetMethod(nameof(ICommandHandler<ICommand>.HandleAsync))
            ?? throw new InvalidOperationException($"HandleAsync missing on {handlerType.Name}.");

        var task = (Task<Result>)method.Invoke(handler, [command, cancellationToken])!;
        return await task.ConfigureAwait(false);
    }

    public async Task<Result<TResponse>> SendAsync<TResponse>(
        ICommand<TResponse> command,
        CancellationToken cancellationToken = default)
    {
        var handlerType = typeof(ICommandHandler<,>).MakeGenericType(command.GetType(), typeof(TResponse));
        var handler = serviceProvider.GetRequiredService(handlerType);
        var method = handlerType.GetMethod("HandleAsync")
            ?? throw new InvalidOperationException($"HandleAsync missing on {handlerType.Name}.");

        var task = (Task<Result<TResponse>>)method.Invoke(handler, [command, cancellationToken])!;
        return await task.ConfigureAwait(false);
    }
}
