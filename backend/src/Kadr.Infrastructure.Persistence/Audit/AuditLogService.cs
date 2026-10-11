using System.Globalization;
using System.Text;
using Kadr.Identity.Domain.Roles;
using Kadr.Identity.Domain.Users;
using Kadr.Platform.Application.Abstractions;
using Kadr.Platform.Application.Audit;
using Kadr.Platform.Domain.Audit;
using Kadr.SharedKernel.Constants;
using Kadr.SharedKernel.Results;
using Kadr.SharedKernel.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Kadr.Infrastructure.Persistence.Audit;

public sealed class AuditLogService(KadrDbContext db, ITenantContext tenantContext) : IAuditLogService
{
    public const int MaxExportRows = 10_000;
    private const int DefaultPageSize = 25;
    private const int MaxPageSize = 100;

    private sealed record Row(
        Guid Id,
        DateTimeOffset At,
        string Action,
        string EntityType,
        Guid? EntityId,
        Guid? UserId,
        string? IpAddress,
        string? UserAgent,
        string? OldValuesJson,
        string? NewValuesJson);

    public async Task<Result<AuditLogListResultDto>> ListAsync(
        AuditLogQuery query,
        CancellationToken cancellationToken = default)
    {
        if (tenantContext.TenantId is null)
        {
            return Result.Failure<AuditLogListResultDto>(ErrorCodes.TenantNotFound, ErrorCodes.TenantNotFound);
        }

        var pageSize = query.PageSize <= 0 ? DefaultPageSize : Math.Clamp(query.PageSize, 1, MaxPageSize);
        var filter = ApplyFilters(db.AuditLogs.AsNoTracking(), query);
        if (filter.IsFailure)
        {
            return Result.Failure<AuditLogListResultDto>(filter.ErrorCode!, filter.ErrorMessage!);
        }

        var raw = await filter.Value!
            .OrderByDescending(a => a.At)
            .ThenByDescending(a => a.Id)
            .Take(pageSize + 1)
            .Select(a => new
            {
                a.Id,
                a.At,
                a.Action,
                a.EntityType,
                a.EntityId,
                a.UserId,
                a.IpAddress,
                a.UserAgent,
                a.OldValuesJson,
                a.NewValuesJson,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var hasMore = raw.Count > pageSize;
        var page = hasMore ? raw.Take(pageSize).ToList() : raw;
        var rows = page.Select(a => new Row(
            a.Id,
            a.At,
            a.Action,
            a.EntityType,
            a.EntityId,
            a.UserId,
            a.IpAddress,
            a.UserAgent,
            a.OldValuesJson,
            a.NewValuesJson)).ToList();

        var items = await MapAsync(rows, cancellationToken).ConfigureAwait(false);
        string? nextCursor = hasMore && rows.Count > 0
            ? EncodeCursor(rows[^1].At, rows[^1].Id)
            : null;

        return Result.Success(new AuditLogListResultDto(items, nextCursor));
    }

    public async Task<Result<byte[]>> ExportCsvAsync(
        AuditLogQuery query,
        CancellationToken cancellationToken = default)
    {
        if (tenantContext.TenantId is null)
        {
            return Result.Failure<byte[]>(ErrorCodes.TenantNotFound, ErrorCodes.TenantNotFound);
        }

        var filter = ApplyFilters(db.AuditLogs.AsNoTracking(), query with { Cursor = null, PageSize = 0 });
        if (filter.IsFailure)
        {
            return Result.Failure<byte[]>(filter.ErrorCode!, filter.ErrorMessage!);
        }

        var count = await filter.Value!.CountAsync(cancellationToken).ConfigureAwait(false);
        if (count > MaxExportRows)
        {
            return Result.Failure<byte[]>(ErrorCodes.AuditExportTooLarge, ErrorCodes.AuditExportTooLarge);
        }

        var raw = await filter.Value!
            .OrderByDescending(a => a.At)
            .ThenByDescending(a => a.Id)
            .Take(MaxExportRows)
            .Select(a => new
            {
                a.Id,
                a.At,
                a.Action,
                a.EntityType,
                a.EntityId,
                a.UserId,
                a.IpAddress,
                a.UserAgent,
                a.OldValuesJson,
                a.NewValuesJson,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var rows = raw.Select(a => new Row(
            a.Id,
            a.At,
            a.Action,
            a.EntityType,
            a.EntityId,
            a.UserId,
            a.IpAddress,
            a.UserAgent,
            a.OldValuesJson,
            a.NewValuesJson)).ToList();

        var items = await MapAsync(rows, cancellationToken).ConfigureAwait(false);
        var sb = new StringBuilder();
        sb.AppendLine(
            "id,at,action,entityType,entityId,entityDisplayName,userId,userName,userEmail,ipAddress,userAgent,oldValuesJson,newValuesJson");
        foreach (var item in items)
        {
            sb.Append(Csv(item.Id.ToString()))
                .Append(',')
                .Append(Csv(item.At.ToString("O", CultureInfo.InvariantCulture)))
                .Append(',')
                .Append(Csv(item.Action))
                .Append(',')
                .Append(Csv(item.EntityType))
                .Append(',')
                .Append(Csv(item.EntityId?.ToString()))
                .Append(',')
                .Append(Csv(item.EntityDisplayName))
                .Append(',')
                .Append(Csv(item.UserId?.ToString()))
                .Append(',')
                .Append(Csv(item.UserName))
                .Append(',')
                .Append(Csv(item.UserEmail))
                .Append(',')
                .Append(Csv(item.IpAddress))
                .Append(',')
                .Append(Csv(item.UserAgent))
                .Append(',')
                .Append(Csv(item.OldValuesJson))
                .Append(',')
                .Append(Csv(item.NewValuesJson))
                .AppendLine();
        }

        return Result.Success(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray());
    }

    private static Result<IQueryable<AuditLog>> ApplyFilters(IQueryable<AuditLog> queryable, AuditLogQuery query)
    {
        if (query.UserId is Guid userId)
        {
            queryable = queryable.Where(a => a.UserId == userId);
        }

        if (!string.IsNullOrWhiteSpace(query.EntityType))
        {
            var entityType = query.EntityType.Trim();
            queryable = queryable.Where(a => a.EntityType == entityType);
        }

        if (!string.IsNullOrWhiteSpace(query.Action))
        {
            var action = query.Action.Trim();
            queryable = queryable.Where(a => a.Action == action);
        }

        if (query.From is DateTimeOffset from)
        {
            queryable = queryable.Where(a => a.At >= from);
        }

        if (query.To is DateTimeOffset to)
        {
            queryable = queryable.Where(a => a.At <= to);
        }

        if (!string.IsNullOrWhiteSpace(query.Cursor))
        {
            if (!TryDecodeCursor(query.Cursor, out var at, out var id))
            {
                return Result.Failure<IQueryable<AuditLog>>(
                    ErrorCodes.AuditCursorInvalid,
                    ErrorCodes.AuditCursorInvalid);
            }

            queryable = queryable.Where(a => a.At < at || (a.At == at && a.Id < id));
        }

        return Result.Success(queryable);
    }

    private async Task<IReadOnlyList<AuditLogListItemDto>> MapAsync(
        IReadOnlyList<Row> rows,
        CancellationToken cancellationToken)
    {
        var userIds = rows.Where(r => r.UserId.HasValue).Select(r => r.UserId!.Value).Distinct().ToList();
        var users = userIds.Count == 0
            ? new Dictionary<Guid, (string Name, string Email)>()
            : await db.Users.AsNoTracking()
                .Where(u => userIds.Contains(u.Id))
                .Select(u => new { u.Id, u.FullName, u.Email })
                .ToDictionaryAsync(u => u.Id, u => (Name: u.FullName, Email: u.Email), cancellationToken)
                .ConfigureAwait(false);

        var roleIds = rows
            .Where(r => r.EntityId.HasValue && IsRoleEntity(r.EntityType))
            .Select(r => r.EntityId!.Value)
            .Distinct()
            .ToList();
        var roles = roleIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await db.Roles.AsNoTracking()
                .Where(r => roleIds.Contains(r.Id))
                .Select(r => new { r.Id, r.Name })
                .ToDictionaryAsync(r => r.Id, r => r.Name, cancellationToken)
                .ConfigureAwait(false);

        var userEntityIds = rows
            .Where(r => r.EntityId.HasValue && IsUserEntity(r.EntityType))
            .Select(r => r.EntityId!.Value)
            .Distinct()
            .ToList();
        var userEntities = userEntityIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await db.Users.AsNoTracking()
                .Where(u => userEntityIds.Contains(u.Id))
                .Select(u => new { u.Id, u.FullName })
                .ToDictionaryAsync(u => u.Id, u => u.FullName, cancellationToken)
                .ConfigureAwait(false);

        return rows.Select(r =>
        {
            string? display = null;
            if (r.EntityId is Guid eid)
            {
                if (roles.TryGetValue(eid, out var roleName))
                {
                    display = roleName;
                }
                else if (userEntities.TryGetValue(eid, out var entityUserName))
                {
                    display = entityUserName;
                }
            }

            string? actorName = null;
            string? actorEmail = null;
            if (r.UserId is Guid uid && users.TryGetValue(uid, out var actor))
            {
                actorName = actor.Name;
                actorEmail = actor.Email;
            }

            return new AuditLogListItemDto(
                r.Id,
                r.At,
                r.Action,
                r.EntityType,
                r.EntityId,
                display,
                r.UserId,
                actorName,
                actorEmail,
                r.IpAddress,
                r.UserAgent,
                r.OldValuesJson,
                r.NewValuesJson);
        }).ToList();
    }

    private static bool IsRoleEntity(string entityType) =>
        string.Equals(entityType, "role", StringComparison.OrdinalIgnoreCase)
        || string.Equals(entityType, nameof(Role), StringComparison.OrdinalIgnoreCase);

    private static bool IsUserEntity(string entityType) =>
        string.Equals(entityType, "user", StringComparison.OrdinalIgnoreCase)
        || string.Equals(entityType, nameof(AppUser), StringComparison.OrdinalIgnoreCase);

    private static string EncodeCursor(DateTimeOffset at, Guid id)
    {
        var raw = $"{at.UtcDateTime:O}|{id:N}";
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(raw));
    }

    private static bool TryDecodeCursor(string cursor, out DateTimeOffset at, out Guid id)
    {
        at = default;
        id = default;
        try
        {
            var raw = Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
            var parts = raw.Split('|', 2);
            if (parts.Length != 2)
            {
                return false;
            }

            if (!DateTimeOffset.TryParse(parts[0], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out at))
            {
                return false;
            }

            return Guid.TryParseExact(parts[1], "N", out id);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static string Csv(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var escaped = value.Replace("\"", "\"\"", StringComparison.Ordinal);
        return $"\"{escaped}\"";
    }
}
