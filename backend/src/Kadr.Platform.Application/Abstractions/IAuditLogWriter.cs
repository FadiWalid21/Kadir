namespace Kadr.Platform.Application.Abstractions;

public interface IAuditLogWriter
{
    Task WriteAsync(
        Guid tenantId,
        Guid? userId,
        string action,
        string entityType,
        Guid? entityId,
        string? oldValuesJson,
        string? newValuesJson,
        CancellationToken cancellationToken = default);
}
