using Kadr.SharedKernel.Results;

namespace Kadr.SharedKernel.Application;

public interface ICommandDispatcher
{
    Task<Result> SendAsync(ICommand command, CancellationToken cancellationToken = default);

    Task<Result<TResponse>> SendAsync<TResponse>(
        ICommand<TResponse> command,
        CancellationToken cancellationToken = default);
}
