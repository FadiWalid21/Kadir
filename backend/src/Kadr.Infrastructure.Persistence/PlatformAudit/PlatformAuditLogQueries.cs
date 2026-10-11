using Kadr.Infrastructure.Persistence.Audit;
using Kadr.Platform.Application.Abstractions;
using Kadr.Platform.Application.PlatformAudit;
using Kadr.SharedKernel.Constants;
using Kadr.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Kadr.Infrastructure.Persistence.PlatformAudit;

/// <summary>
/// Cross-tenant audit feed for the platform admin console. Sanctioned IgnoreQueryFilters use: reachable only
/// through the PlatformAdmin-guarded endpoint; every other tenant-owned lookup here is keyed by tenant id.
/// </summary>
public sealed class PlatformAuditLogQueries(KadrDbContext db) : IPlatformAuditLogQueries
{
    public async Task<Result<PlatformAuditLogListResultDto>> ListAsync(
        PlatformAuditLogQuery query,
        CancellationToken cancellationToken = default)
    {
        var pageSize = query.PageSize <= 0
            ? PlatformAuditLogLimits.DefaultPageSize
            : Math.Clamp(query.PageSize, 1, PlatformAuditLogLimits.MaxPageSize);

        var queryable = db.AuditLogs.IgnoreQueryFilters().AsNoTracking();

        if (query.TenantId is Guid tenantId)
        {
            queryable = queryable.Where(a => a.TenantId == tenantId);
        }

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
            if (!AuditCursor.TryDecode(query.Cursor, out var at, out var id))
            {
                return Result.Failure<PlatformAuditLogListResultDto>(
                    ErrorCodes.AuditCursorInvalid,
                    ErrorCodes.AuditCursorInvalid);
            }

            queryable = queryable.Where(a => a.At < at || (a.At == at && a.Id < id));
        }

        var raw = await queryable
            .OrderByDescending(a => a.At)
            .ThenByDescending(a => a.Id)
            .Take(pageSize + 1)
            .Select(a => new
            {
                a.Id,
                a.At,
                a.TenantId,
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

        var tenantIds = page.Select(r => r.TenantId).Distinct().ToList();
        var tenants = tenantIds.Count == 0
            ? new Dictionary<Guid, (string Name, string Slug)>()
            : await db.Tenants.AsNoTracking()
                .Where(t => tenantIds.Contains(t.Id))
                .Select(t => new { t.Id, t.Name, t.Slug })
                .ToDictionaryAsync(t => t.Id, t => (t.Name, t.Slug), cancellationToken)
                .ConfigureAwait(false);

        static bool IsPlatformAction(string actionCode) =>
            actionCode.StartsWith(PlatformAuditActions.Prefix, StringComparison.Ordinal);

        var tenantActorIds = page
            .Where(r => r.UserId != null && !IsPlatformAction(r.Action))
            .Select(r => r.UserId!.Value)
            .Distinct()
            .ToList();
        var tenantActors = tenantActorIds.Count == 0
            ? new Dictionary<(Guid TenantId, Guid UserId), (string Name, string Email)>()
            : (await db.Users.IgnoreQueryFilters().AsNoTracking()
                    .Where(u => tenantIds.Contains(u.TenantId) && tenantActorIds.Contains(u.Id))
                    .Select(u => new { u.TenantId, u.Id, u.FullName, u.Email })
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false))
                .ToDictionary(u => (u.TenantId, u.Id), u => (Name: u.FullName, Email: u.Email));

        var adminIds = page
            .Where(r => r.UserId != null && IsPlatformAction(r.Action))
            .Select(r => r.UserId!.Value)
            .Distinct()
            .ToList();
        var admins = adminIds.Count == 0
            ? new Dictionary<Guid, (string Name, string Email)>()
            : await db.PlatformAdmins.AsNoTracking()
                .Where(a => adminIds.Contains(a.Id))
                .ToDictionaryAsync(a => a.Id, a => (Name: a.FullName, Email: a.Email), cancellationToken)
                .ConfigureAwait(false);

        var items = page
            .Select(r =>
            {
                var platformAction = IsPlatformAction(r.Action);
                string? actorName = null;
                string? actorEmail = null;
                if (r.UserId is Guid uid)
                {
                    if (platformAction)
                    {
                        if (admins.TryGetValue(uid, out var admin))
                        {
                            (actorName, actorEmail) = admin;
                        }
                    }
                    else if (tenantActors.TryGetValue((r.TenantId, uid), out var actor))
                    {
                        (actorName, actorEmail) = actor;
                    }
                }

                var hasTenant = tenants.TryGetValue(r.TenantId, out var tenant);
                return new PlatformAuditLogItemDto(
                    r.Id,
                    r.At,
                    r.TenantId,
                    hasTenant ? tenant.Name : null,
                    hasTenant ? tenant.Slug : null,
                    r.Action,
                    r.EntityType,
                    r.EntityId,
                    r.UserId,
                    platformAction ? PlatformAuditActorKinds.PlatformAdmin : PlatformAuditActorKinds.TenantUser,
                    actorName,
                    actorEmail,
                    r.IpAddress,
                    r.UserAgent,
                    r.OldValuesJson,
                    r.NewValuesJson);
            })
            .ToList();

        var nextCursor = hasMore && page.Count > 0
            ? AuditCursor.Encode(page[^1].At, page[^1].Id)
            : null;

        return Result.Success(new PlatformAuditLogListResultDto(items, nextCursor));
    }
}
