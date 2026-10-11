using Kadr.Platform.Application.Abstractions;
using Kadr.Platform.Domain.Audit;

namespace Kadr.Infrastructure.Persistence.Audit;

public sealed class AuditLogWriter(KadrDbContext db, IClientRequestInfo clientInfo) : IAuditLogWriter
{
    public void Stage(
        Guid tenantId,
        Guid? userId,
        string action,
        string entityType,
        Guid? entityId,
        string? oldValuesJson,
        string? newValuesJson,
        string? ipAddress = null,
        string? userAgent = null)
    {
        db.AuditLogs.Add(AuditLog.Create(
            tenantId,
            userId,
            action,
            entityType,
            entityId,
            oldValuesJson,
            newValuesJson,
            ipAddress ?? clientInfo.IpAddress,
            userAgent ?? clientInfo.UserAgent));
    }

    public async Task WriteAsync(
        Guid tenantId,
        Guid? userId,
        string action,
        string entityType,
        Guid? entityId,
        string? oldValuesJson,
        string? newValuesJson,
        CancellationToken cancellationToken = default,
        string? ipAddress = null,
        string? userAgent = null)
    {
        Stage(tenantId, userId, action, entityType, entityId, oldValuesJson, newValuesJson, ipAddress, userAgent);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
