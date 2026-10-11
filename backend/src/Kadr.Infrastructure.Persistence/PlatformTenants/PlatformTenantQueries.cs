using System.Text.Json;
using Kadr.Platform.Application.Abstractions;
using Kadr.Platform.Application.PlatformTenants;
using Kadr.Platform.Application.Subscriptions;
using Kadr.Platform.Domain.Subscriptions;
using Kadr.Platform.Domain.Tenants;
using Kadr.SharedKernel.Constants;
using Kadr.SharedKernel.Results;
using Kadr.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace Kadr.Infrastructure.Persistence.PlatformTenants;

/// <summary>
/// Cross-tenant reads for the platform admin console. Sanctioned IgnoreQueryFilters use: every call here is
/// reachable only through endpoints guarded by the PlatformAdmin policy, and tenant-owned queries always
/// carry an explicit TenantId predicate.
/// </summary>
public sealed class PlatformTenantQueries(
    KadrDbContext db,
    IActiveEmployeeCounter employeeCounter,
    IClock clock) : IPlatformTenantQueries
{
    private const int RecentEventCount = 10;
    private const int PlatformActionCount = 10;

    private static readonly string[] CategoryOrder =
    [
        "users", "roles", "invitations", "subscription", "templates", "settings", "auth", "other",
    ];

    private sealed class TenantRow
    {
        public Guid Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public string Slug { get; init; } = string.Empty;
        public string IndustryCode { get; init; } = string.Empty;
        public TenantStatus Status { get; init; }
        public string PlanCode { get; init; } = string.Empty;
        public string PlanNameAr { get; init; } = string.Empty;
        public string PlanNameEn { get; init; } = string.Empty;
        public SubscriptionStatus SubscriptionStatus { get; init; }
        public DateTimeOffset? TrialEndsAt { get; init; }
        public DateTimeOffset CreatedAt { get; init; }
    }

    public async Task<Result<PlatformTenantListResultDto>> ListAsync(
        PlatformTenantListQuery query,
        CancellationToken cancellationToken = default)
    {
        TenantStatus? status = null;
        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            if (!Enum.TryParse<TenantStatus>(query.Status.Trim(), ignoreCase: true, out var parsed)
                || !Enum.IsDefined(parsed))
            {
                return Result.Failure<PlatformTenantListResultDto>(
                    ErrorCodes.PlatformValidationFailed,
                    ErrorCodes.PlatformValidationFailed);
            }

            status = parsed;
        }

        var page = Math.Max(1, query.Page);
        var pageSize = query.PageSize <= 0
            ? PlatformTenantFilters.DefaultPageSize
            : Math.Clamp(query.PageSize, 1, PlatformTenantFilters.MaxPageSize);

        var filtered = Rows();

        if (!string.IsNullOrWhiteSpace(query.Q))
        {
            var pattern = $"%{EscapeLike(query.Q.Trim())}%";
            filtered = filtered.Where(r =>
                EF.Functions.ILike(r.Name, pattern, "\\") || EF.Functions.ILike(r.Slug, pattern, "\\"));
        }

        if (status is { } statusFilter)
        {
            filtered = filtered.Where(r => r.Status == statusFilter);
        }

        if (!string.IsNullOrWhiteSpace(query.Plan))
        {
            var plan = query.Plan.Trim();
            filtered = filtered.Where(r => r.PlanCode == plan);
        }

        if (!string.IsNullOrWhiteSpace(query.Industry))
        {
            var industry = query.Industry.Trim().ToLowerInvariant();
            filtered = filtered.Where(r => r.IndustryCode == industry);
        }

        var total = await filtered.CountAsync(cancellationToken).ConfigureAwait(false);
        var rows = await filtered
            .OrderByDescending(r => r.CreatedAt)
            .ThenBy(r => r.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var ids = rows.Select(r => r.Id).ToList();
        var userCounts = ids.Count == 0
            ? new Dictionary<Guid, int>()
            : await db.Users.IgnoreQueryFilters().AsNoTracking()
                .Where(u => ids.Contains(u.TenantId))
                .GroupBy(u => u.TenantId)
                .Select(g => new { TenantId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.TenantId, x => x.Count, cancellationToken)
                .ConfigureAwait(false);

        var employeeCounts = new Dictionary<Guid, int>();
        foreach (var id in ids)
        {
            employeeCounts[id] = await employeeCounter.CountAsync(id, cancellationToken).ConfigureAwait(false);
        }

        var items = rows
            .Select(r => new PlatformTenantListItemDto(
                r.Id,
                r.Name,
                r.Slug,
                r.IndustryCode,
                r.Status.ToString(),
                r.PlanCode,
                r.PlanNameAr,
                r.PlanNameEn,
                r.SubscriptionStatus.ToString(),
                r.TrialEndsAt,
                userCounts.GetValueOrDefault(r.Id),
                employeeCounts.GetValueOrDefault(r.Id),
                r.CreatedAt))
            .ToList();

        var summary = await BuildSummaryAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success(new PlatformTenantListResultDto(items, page, pageSize, total, summary));
    }

    public async Task<Result<PlatformTenantDetailDto>> GetAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var tenant = await db.Tenants.AsNoTracking()
            .Where(t => t.Id == tenantId)
            .Select(t => new
            {
                t.Name,
                t.Slug,
                t.IndustryCode,
                t.DefaultLanguage,
                t.TimeZone,
                t.Status,
                HasLogo = t.LogoFileId != null,
                t.CreatedAt,
                t.TrialEndsAt,
            })
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (tenant is null)
        {
            return Result.Failure<PlatformTenantDetailDto>(ErrorCodes.TenantNotFound, ErrorCodes.TenantNotFound);
        }

        var owner = await (
                from ur in db.UserRoles.IgnoreQueryFilters().AsNoTracking()
                join r in db.Roles.IgnoreQueryFilters().AsNoTracking() on ur.RoleId equals r.Id
                join u in db.Users.IgnoreQueryFilters().AsNoTracking() on ur.UserId equals u.Id
                where ur.TenantId == tenantId
                      && r.TenantId == tenantId
                      && u.TenantId == tenantId
                      && r.IsSystem
                      && r.Name == SystemRoleNames.Owner
                orderby u.CreatedAt, u.Id
                select new PlatformTenantOwnerDto(u.Id, u.FullName, u.Email, u.IsActive, u.LastLoginAt))
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        var userCounts = await db.Users.IgnoreQueryFilters().AsNoTracking()
            .Where(u => u.TenantId == tenantId)
            .GroupBy(u => 1)
            .Select(g => new { Total = g.Count(), Active = g.Count(u => u.IsActive) })
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        var subscription = await (
                from s in db.Subscriptions.IgnoreQueryFilters().AsNoTracking()
                join p in db.Plans.AsNoTracking() on s.PlanId equals p.Id
                where s.TenantId == tenantId
                select new
                {
                    p.Code,
                    p.NameAr,
                    p.NameEn,
                    p.PricingModel,
                    p.EmployeeCap,
                    s.Status,
                    s.StartsAt,
                    s.TrialEndsAt,
                    s.RenewsAt,
                    s.CancelledAt,
                })
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        var activeEmployees = await employeeCounter.CountAsync(tenantId, cancellationToken).ConfigureAwait(false);

        PlatformTenantSubscriptionDto? subscriptionDto = null;
        if (subscription is not null)
        {
            var (state, readOnly, daysLeft) = SubscriptionStateCalculator.Compute(
                subscription.Status,
                subscription.TrialEndsAt,
                subscription.CancelledAt,
                clock.UtcNow);

            subscriptionDto = new PlatformTenantSubscriptionDto(
                subscription.Code,
                subscription.NameAr,
                subscription.NameEn,
                subscription.PricingModel.ToString(),
                subscription.EmployeeCap,
                subscription.Status.ToString(),
                state,
                readOnly,
                subscription.StartsAt,
                subscription.TrialEndsAt,
                subscription.RenewsAt,
                subscription.CancelledAt,
                daysLeft);
        }

        return Result.Success(new PlatformTenantDetailDto(
            tenantId,
            new PlatformTenantCompanyDto(
                tenant.Name,
                tenant.Slug,
                tenant.IndustryCode,
                tenant.DefaultLanguage.ToString(),
                tenant.TimeZone,
                tenant.Status.ToString(),
                tenant.HasLogo,
                tenant.CreatedAt,
                tenant.TrialEndsAt),
            owner,
            new PlatformTenantUsageDto(
                userCounts?.Total ?? 0,
                userCounts?.Active ?? 0,
                activeEmployees,
                subscription?.EmployeeCap),
            subscriptionDto));
    }

    public async Task<Result<PlatformTenantAuditSummaryDto>> GetAuditSummaryAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var exists = await db.Tenants.AsNoTracking()
            .AnyAsync(t => t.Id == tenantId, cancellationToken)
            .ConfigureAwait(false);
        if (!exists)
        {
            return Result.Failure<PlatformTenantAuditSummaryDto>(
                ErrorCodes.TenantNotFound,
                ErrorCodes.TenantNotFound);
        }

        var now = clock.UtcNow;
        var since7 = now.AddDays(-7);
        var since30 = now.AddDays(-30);

        var tenantAudit = db.AuditLogs.IgnoreQueryFilters().AsNoTracking()
            .Where(a => a.TenantId == tenantId && !a.Action.StartsWith(PlatformAuditActions.Prefix));

        var grouped = await tenantAudit
            .Where(a => a.At >= since30)
            .GroupBy(a => new { a.Action, a.EntityType })
            .Select(g => new
            {
                g.Key.Action,
                g.Key.EntityType,
                Last30 = g.Count(),
                Last7 = g.Count(a => a.At >= since7),
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var totals = CategoryOrder.ToDictionary(c => c, _ => (Last7: 0, Last30: 0));
        foreach (var row in grouped)
        {
            var category = CategoryOf(row.Action, row.EntityType);
            var current = totals[category];
            totals[category] = (current.Last7 + row.Last7, current.Last30 + row.Last30);
        }

        var categories = CategoryOrder
            .Select(c => new PlatformAuditCategoryCountDto(c, totals[c].Last7, totals[c].Last30))
            .ToList();

        var recent = await tenantAudit
            .OrderByDescending(a => a.At)
            .ThenByDescending(a => a.Id)
            .Take(RecentEventCount)
            .Select(a => new { a.Id, a.At, a.Action, a.EntityType, a.UserId })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var userIds = recent.Where(a => a.UserId != null).Select(a => a.UserId!.Value).Distinct().ToList();
        var userNames = userIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await db.Users.IgnoreQueryFilters().AsNoTracking()
                .Where(u => u.TenantId == tenantId && userIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.FullName, cancellationToken)
                .ConfigureAwait(false);

        var recentDtos = recent
            .Select(a => new PlatformTenantEventDto(
                a.Id,
                a.At,
                a.Action,
                a.EntityType,
                a.UserId,
                a.UserId is { } uid && userNames.TryGetValue(uid, out var name) ? name : null))
            .ToList();

        var platformRows = await db.AuditLogs.IgnoreQueryFilters().AsNoTracking()
            .Where(a => a.TenantId == tenantId && a.Action.StartsWith(PlatformAuditActions.Prefix))
            .OrderByDescending(a => a.At)
            .ThenByDescending(a => a.Id)
            .Take(PlatformActionCount)
            .Select(a => new { a.Id, a.At, a.Action, a.UserId, a.NewValuesJson })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var adminIds = platformRows.Where(a => a.UserId != null).Select(a => a.UserId!.Value).Distinct().ToList();
        var admins = adminIds.Count == 0
            ? new Dictionary<Guid, (string Name, string Email)>()
            : await db.PlatformAdmins.AsNoTracking()
                .Where(a => adminIds.Contains(a.Id))
                .ToDictionaryAsync(a => a.Id, a => (Name: a.FullName, Email: a.Email), cancellationToken)
                .ConfigureAwait(false);

        var platformDtos = platformRows
            .Select(a =>
            {
                string? adminName = null;
                string? adminEmail = null;
                if (a.UserId is { } adminId && admins.TryGetValue(adminId, out var admin))
                {
                    adminName = admin.Name;
                    adminEmail = admin.Email;
                }

                return new PlatformActionDto(
                    a.Id,
                    a.At,
                    a.Action,
                    a.UserId,
                    adminName,
                    adminEmail,
                    ReadReason(a.NewValuesJson));
            })
            .ToList();

        return Result.Success(new PlatformTenantAuditSummaryDto(categories, recentDtos, platformDtos));
    }

    private IQueryable<TenantRow> Rows() =>
        from t in db.Tenants.AsNoTracking()
        join s in db.Subscriptions.IgnoreQueryFilters().AsNoTracking() on t.Id equals s.TenantId
        join p in db.Plans.AsNoTracking() on s.PlanId equals p.Id
        select new TenantRow
        {
            Id = t.Id,
            Name = t.Name,
            Slug = t.Slug,
            IndustryCode = t.IndustryCode,
            Status = t.Status,
            PlanCode = p.Code,
            PlanNameAr = p.NameAr,
            PlanNameEn = p.NameEn,
            SubscriptionStatus = s.Status,
            TrialEndsAt = s.TrialEndsAt,
            CreatedAt = t.CreatedAt,
        };

    private async Task<PlatformTenantSummaryDto> BuildSummaryAsync(CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var soon = now.AddDays(PlatformTenantFilters.ExpiringSoonDays);

        var summary = await Rows()
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Total = g.Count(),
                Trial = g.Count(r => r.Status == TenantStatus.Trial),
                Expiring = g.Count(r =>
                    r.SubscriptionStatus == SubscriptionStatus.Trialing
                    && r.TrialEndsAt != null
                    && r.TrialEndsAt > now
                    && r.TrialEndsAt <= soon),
                Suspended = g.Count(r => r.Status == TenantStatus.Suspended),
            })
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return summary is null
            ? new PlatformTenantSummaryDto(0, 0, 0, 0)
            : new PlatformTenantSummaryDto(summary.Total, summary.Trial, summary.Expiring, summary.Suspended);
    }

    private static string EscapeLike(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);

    private static string CategoryOf(string action, string entityType)
    {
        if (action.StartsWith("auth.", StringComparison.OrdinalIgnoreCase)
            || action.StartsWith("login", StringComparison.OrdinalIgnoreCase)
            || action.StartsWith("password", StringComparison.OrdinalIgnoreCase))
        {
            return "auth";
        }

        if (action.StartsWith("subscription.", StringComparison.OrdinalIgnoreCase))
        {
            return "subscription";
        }

        if (action.StartsWith("template.", StringComparison.OrdinalIgnoreCase))
        {
            return "templates";
        }

        return entityType.ToLowerInvariant() switch
        {
            "appuser" or "user" or "users" => "users",
            "role" or "roles" or "rolepermission" or "userrole" => "roles",
            "invitation" or "invitations" => "invitations",
            "subscription" or "plan" => "subscription",
            "shift" or "shifts" or "leavetype" or "leave-types" or "documenttype" or "document-types"
                or "jobtitle" or "job-titles" or "department" or "departments" or "templates" => "templates",
            "tenant" or "tenantsetting" or "legalentity" or "branch" or "fileobject" => "settings",
            _ => "other",
        };
    }

    private static string? ReadReason(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.ValueKind == JsonValueKind.Object
                   && doc.RootElement.TryGetProperty("reason", out var reason)
                   && reason.ValueKind == JsonValueKind.String
                ? reason.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
