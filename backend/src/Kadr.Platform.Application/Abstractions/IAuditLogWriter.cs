namespace Kadr.Platform.Application.Abstractions;

public interface IAuditLogWriter
{
    /// <summary>Adds an audit row to the current DbContext without saving (for same-unit SaveChanges).</summary>
    void Stage(
        Guid tenantId,
        Guid? userId,
        string action,
        string entityType,
        Guid? entityId,
        string? oldValuesJson,
        string? newValuesJson,
        string? ipAddress = null,
        string? userAgent = null);

    Task WriteAsync(
        Guid tenantId,
        Guid? userId,
        string action,
        string entityType,
        Guid? entityId,
        string? oldValuesJson,
        string? newValuesJson,
        CancellationToken cancellationToken = default,
        string? ipAddress = null,
        string? userAgent = null);
}
