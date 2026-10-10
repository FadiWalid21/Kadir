using Kadr.Platform.Domain.Notes;
using Kadr.SharedKernel.Application;
using Microsoft.Extensions.Logging;

namespace Kadr.Platform.Application.Notes;

public sealed class TenantNoteCreatedHandler(ILogger<TenantNoteCreatedHandler> logger)
    : IDomainEventHandler<TenantNoteCreated>
{
    private static int _handleCount;

    public static int HandleCount => Volatile.Read(ref _handleCount);

    public static void ResetHandleCount() => Volatile.Write(ref _handleCount, 0);

    public Task HandleAsync(TenantNoteCreated domainEvent, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _handleCount);
        logger.LogInformation(
            "Tenant note created {NoteId} for tenant {TenantId}",
            domainEvent.NoteId,
            domainEvent.TenantId);
        return Task.CompletedTask;
    }
}
