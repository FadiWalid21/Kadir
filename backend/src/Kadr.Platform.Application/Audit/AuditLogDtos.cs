namespace Kadr.Platform.Application.Audit;

public sealed record AuditLogListItemDto(
    Guid Id,
    DateTimeOffset At,
    string Action,
    string EntityType,
    Guid? EntityId,
    string? EntityDisplayName,
    Guid? UserId,
    string? UserName,
    string? UserEmail,
    string? IpAddress,
    string? UserAgent,
    string? OldValuesJson,
    string? NewValuesJson);

public sealed record AuditLogListResultDto(
    IReadOnlyList<AuditLogListItemDto> Items,
    string? NextCursor);

public sealed record AuditLogQuery(
    Guid? UserId,
    string? EntityType,
    string? Action,
    DateTimeOffset? From,
    DateTimeOffset? To,
    int PageSize,
    string? Cursor);
