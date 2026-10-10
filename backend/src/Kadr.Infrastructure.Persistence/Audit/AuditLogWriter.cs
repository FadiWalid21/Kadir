using Kadr.Platform.Application.Abstractions;
using Kadr.Platform.Domain.Audit;

namespace Kadr.Infrastructure.Persistence.Audit;

public sealed class AuditLogWriter(KadrDbContext db) : IAuditLogWriter
{
    public async Task WriteAsync(
        Guid tenantId,
        Guid? userId,
        string action,
        string entityType,
        Guid? entityId,
        string? oldValuesJson,
        string? newValuesJson,
        CancellationToken cancellationToken = default)
    {
        db.AuditLogs.Add(AuditLog.Create(
            tenantId,
            userId,
            action,
            entityType,
            entityId,
            oldValuesJson,
            newValuesJson));
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
