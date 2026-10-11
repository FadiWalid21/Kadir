using System.Text.Json;
using Kadr.Platform.Application.Abstractions;
using Kadr.Platform.Domain.Attendance;
using Kadr.Platform.Domain.Employees;
using Kadr.Platform.Domain.Subscriptions;
using Kadr.Platform.Domain.Tenants;
using Kadr.SharedKernel.Constants;
using Kadr.SharedKernel.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Kadr.Infrastructure.Persistence.Attendance;

public sealed class AttendanceAbsenceRunner(
    IServiceScopeFactory scopeFactory,
    ILogger<AttendanceAbsenceRunner> logger) : IAttendanceAbsenceRunner
{
    private const int TenantBatchSize = 50;

    public async Task RunOnceAsync(CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KadrDbContext>();
        var tenantAccessor = scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();

        var tenantIds = await db.Tenants
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(t => t.Status == TenantStatus.Active || t.Status == TenantStatus.Trial)
            .OrderBy(t => t.Id)
            .Select(t => t.Id)
            .Take(500)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var processed = 0;
        foreach (var batch in tenantIds.Chunk(TenantBatchSize))
        {
            foreach (var tenantId in batch)
            {
                tenantAccessor.Set(tenantId);
                try
                {
                    processed += await ProcessTenantAsync(db, tenantId, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "Attendance absence job failed for tenant {TenantId}", tenantId);
                }
                finally
                {
                    tenantAccessor.Clear();
                }
            }
        }

        if (processed > 0)
        {
            logger.LogInformation("Attendance absence job marked {Count} absences", processed);
        }
    }

    private async Task<int> ProcessTenantAsync(
        KadrDbContext db,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var hasFeature = await db.Subscriptions
            .AsNoTracking()
            .AnyAsync(
                s => s.TenantId == tenantId
                     && (s.Status == SubscriptionStatus.Active || s.Status == SubscriptionStatus.Trialing),
                cancellationToken)
            .ConfigureAwait(false);
        if (!hasFeature)
        {
            return 0;
        }

        var tenant = await db.Tenants.AsNoTracking()
            .SingleAsync(t => t.Id == tenantId, cancellationToken)
            .ConfigureAwait(false);

        var absentAfterHours = await ReadIntAsync(
                db,
                SettingKeys.AttendanceAbsentAfterStartHours,
                AttendanceRuleSettings.Defaults.AbsentAfterStartHours,
                cancellationToken)
            .ConfigureAwait(false);
        var weekend = await ReadStringArrayAsync(db, SettingKeys.WorkWeekWeekend, ["fri", "sat"], cancellationToken)
            .ConfigureAwait(false);
        var defaultShiftId = await ReadGuidAsync(db, SettingKeys.AttendanceDefaultShiftId, cancellationToken)
            .ConfigureAwait(false);

        var now = DateTimeOffset.UtcNow;
        var today = TenantTimeZones.ToWorkDate(now, tenant.TimeZone);
        var lookbackStart = today.AddDays(-3);

        var employees = await db.Employees.AsNoTracking()
            .Where(e => e.Status == EmployeeStatus.Active || e.Status == EmployeeStatus.Probation)
            .Select(e => e.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var holidays = await db.Holidays.AsNoTracking()
            .Where(h => h.IsActive)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var marked = 0;
        foreach (var employeeId in employees)
        {
            for (var d = lookbackStart; d <= today; d = d.AddDays(1))
            {
                if (IsWeekend(d, weekend) || holidays.Any(h => h.Covers(d)))
                {
                    continue;
                }

                var assignment = await db.ShiftAssignments.AsNoTracking()
                    .SingleOrDefaultAsync(a => a.EmployeeId == employeeId && a.WorkDate == d, cancellationToken)
                    .ConfigureAwait(false);
                if (assignment is { IsDayOff: true })
                {
                    continue;
                }

                var shiftId = assignment?.ShiftId ?? defaultShiftId;
                if (shiftId is null)
                {
                    continue;
                }

                var shift = await db.Shifts.AsNoTracking()
                    .SingleOrDefaultAsync(s => s.Id == shiftId, cancellationToken)
                    .ConfigureAwait(false);
                if (shift is null)
                {
                    continue;
                }

                var window = AttendanceRulesEngine.BuildWindow(
                    shift.Id,
                    d,
                    shift.StartTime,
                    shift.EndTime,
                    shift.CrossesMidnight,
                    shift.BreakMinutes,
                    tenant.TimeZone);

                var threshold = window.ShiftStartUtc.AddHours(absentAfterHours);
                if (now < threshold || now < window.ShiftEndUtc)
                {
                    continue;
                }

                var hasIn = await db.AttendancePunches.AsNoTracking()
                    .AnyAsync(
                        p => p.EmployeeId == employeeId && p.WorkDate == d && p.Kind == PunchKind.In,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (hasIn)
                {
                    continue;
                }

                var summary = await db.AttendanceDaySummaries
                    .SingleOrDefaultAsync(s => s.EmployeeId == employeeId && s.WorkDate == d, cancellationToken)
                    .ConfigureAwait(false);

                if (summary is { IsManualOverride: true })
                {
                    continue;
                }

                if (summary is null)
                {
                    summary = AttendanceDaySummary.Create(
                        tenantId,
                        employeeId,
                        d,
                        shift.Id,
                        AttendanceDayStatus.Absent);
                    db.AttendanceDaySummaries.Add(summary);
                }

                if (summary.Status == AttendanceDayStatus.Absent && summary.ClosedAt is not null)
                {
                    continue;
                }

                summary.MarkAbsent();
                marked++;
            }
        }

        if (marked > 0)
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            logger.LogInformation("Marked absences for tenant {TenantId}: {Count}", tenantId, marked);
        }

        return marked;
    }

    private static bool IsWeekend(DateOnly date, string[] weekend)
    {
        var dow = date.DayOfWeek switch
        {
            DayOfWeek.Sunday => "sun",
            DayOfWeek.Monday => "mon",
            DayOfWeek.Tuesday => "tue",
            DayOfWeek.Wednesday => "wed",
            DayOfWeek.Thursday => "thu",
            DayOfWeek.Friday => "fri",
            _ => "sat",
        };
        return weekend.Contains(dow, StringComparer.OrdinalIgnoreCase);
    }

    private static async Task<int> ReadIntAsync(
        KadrDbContext db,
        string key,
        int fallback,
        CancellationToken cancellationToken)
    {
        var raw = await db.TenantSettings.AsNoTracking()
            .Where(s => s.Key == key)
            .Select(s => s.ValueJson)
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return fallback;
        }

        try
        {
            return JsonSerializer.Deserialize<int>(raw);
        }
        catch
        {
            return fallback;
        }
    }

    private static async Task<Guid?> ReadGuidAsync(KadrDbContext db, string key, CancellationToken cancellationToken)
    {
        var raw = await db.TenantSettings.AsNoTracking()
            .Where(s => s.Key == key)
            .Select(s => s.ValueJson)
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(raw) || raw is "null")
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<Guid?>(raw);
        }
        catch
        {
            return Guid.TryParse(raw.Trim('"'), out var g) ? g : null;
        }
    }

    private static async Task<string[]> ReadStringArrayAsync(
        KadrDbContext db,
        string key,
        string[] fallback,
        CancellationToken cancellationToken)
    {
        var raw = await db.TenantSettings.AsNoTracking()
            .Where(s => s.Key == key)
            .Select(s => s.ValueJson)
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return fallback;
        }

        try
        {
            return JsonSerializer.Deserialize<string[]>(raw) ?? fallback;
        }
        catch
        {
            return fallback;
        }
    }
}
