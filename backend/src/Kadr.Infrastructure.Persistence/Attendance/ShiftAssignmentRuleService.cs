using System.Text.Json;
using Kadr.Platform.Application.Abstractions;
using Kadr.Platform.Application.Attendance;
using Kadr.Platform.Domain.Attendance;
using Kadr.Platform.Domain.Employees;
using Kadr.SharedKernel.Constants;
using Kadr.SharedKernel.Results;
using Kadr.SharedKernel.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Kadr.Infrastructure.Persistence.Attendance;

internal sealed class ShiftAssignmentRuleService(
    KadrDbContext db,
    ITenantContext tenantContext,
    ISubscriptionGuard subscriptionGuard,
    IAuditLogWriter audit) : IShiftAssignmentRuleService
{
    private const int MaxPageSize = 100;

    public async Task<Result<PagedShiftAssignmentRulesDto>> ListAsync(
        int page,
        int pageSize,
        string? targetType,
        CancellationToken cancellationToken = default)
    {
        var gate = await subscriptionGuard.EnsureFeatureAsync(FeatureKeys.Attendance, cancellationToken)
            .ConfigureAwait(false);
        if (gate.IsFailure)
        {
            return Result.Failure<PagedShiftAssignmentRulesDto>(gate.ErrorCode!, gate.ErrorMessage!);
        }

        if (tenantContext.TenantId is null)
        {
            return Result.Failure<PagedShiftAssignmentRulesDto>(ErrorCodes.TenantNotFound, ErrorCodes.TenantNotFound);
        }

        page = page < 1 ? 1 : page;
        pageSize = pageSize < 1 ? 20 : Math.Min(pageSize, MaxPageSize);

        var query = db.ShiftAssignmentRules.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(targetType)
            && Enum.TryParse<ShiftAssignmentTargetType>(targetType, true, out var tt))
        {
            query = query.Where(r => r.TargetType == tt);
        }

        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var rows = await query
            .OrderByDescending(r => r.EffectiveFrom)
            .ThenBy(r => r.TargetType)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var mapped = await MapManyAsync(rows, cancellationToken).ConfigureAwait(false);
        return Result.Success(new PagedShiftAssignmentRulesDto(mapped, page, pageSize, total));
    }

    public async Task<Result<IReadOnlyList<ShiftAssignmentRuleDto>>> CreateAsync(
        CreateShiftAssignmentRulesRequest request,
        CancellationToken cancellationToken = default)
    {
        var writable = await subscriptionGuard.EnsureWritableAsync(cancellationToken).ConfigureAwait(false);
        if (writable.IsFailure)
        {
            return Result.Failure<IReadOnlyList<ShiftAssignmentRuleDto>>(writable.ErrorCode!, writable.ErrorMessage!);
        }

        var gate = await subscriptionGuard.EnsureFeatureAsync(FeatureKeys.Attendance, cancellationToken)
            .ConfigureAwait(false);
        if (gate.IsFailure)
        {
            return Result.Failure<IReadOnlyList<ShiftAssignmentRuleDto>>(gate.ErrorCode!, gate.ErrorMessage!);
        }

        if (tenantContext.TenantId is null || tenantContext.UserId is null)
        {
            return Result.Failure<IReadOnlyList<ShiftAssignmentRuleDto>>(
                ErrorCodes.TenantNotFound,
                ErrorCodes.TenantNotFound);
        }

        if (!Enum.TryParse<ShiftAssignmentTargetType>(request.TargetType, true, out var targetType))
        {
            return Result.Failure<IReadOnlyList<ShiftAssignmentRuleDto>>(
                ErrorCodes.AttendanceShiftRuleValidationFailed,
                ErrorCodes.AttendanceShiftRuleValidationFailed);
        }

        if (request.TargetIds is null || request.TargetIds.Count == 0)
        {
            return Result.Failure<IReadOnlyList<ShiftAssignmentRuleDto>>(
                ErrorCodes.AttendanceShiftRuleValidationFailed,
                ErrorCodes.AttendanceShiftRuleValidationFailed);
        }

        if (!request.IsDayOff && request.ShiftId is null)
        {
            return Result.Failure<IReadOnlyList<ShiftAssignmentRuleDto>>(
                ErrorCodes.AttendanceShiftRuleValidationFailed,
                ErrorCodes.AttendanceShiftRuleValidationFailed);
        }

        if (request.EffectiveTo is not null && request.EffectiveTo < request.EffectiveFrom)
        {
            return Result.Failure<IReadOnlyList<ShiftAssignmentRuleDto>>(
                ErrorCodes.AttendanceShiftRuleValidationFailed,
                ErrorCodes.AttendanceShiftRuleValidationFailed);
        }

        var today = await TodayTenantAsync(cancellationToken).ConfigureAwait(false);
        if (request.EffectiveFrom < today && !request.ConfirmRecalc)
        {
            return Result.Failure<IReadOnlyList<ShiftAssignmentRuleDto>>(
                ErrorCodes.AttendanceShiftRuleBackdateRequiresConfirm,
                ErrorCodes.AttendanceShiftRuleBackdateRequiresConfirm);
        }

        if (request.ShiftId is Guid sid)
        {
            var shiftOk = await db.Shifts.AsNoTracking()
                .AnyAsync(s => s.Id == sid && s.IsActive, cancellationToken)
                .ConfigureAwait(false);
            if (!shiftOk)
            {
                return Result.Failure<IReadOnlyList<ShiftAssignmentRuleDto>>(
                    ErrorCodes.AttendanceShiftRuleValidationFailed,
                    ErrorCodes.AttendanceShiftRuleValidationFailed);
            }
        }

        var targetCheck = await TargetsExistAsync(targetType, request.TargetIds, cancellationToken)
            .ConfigureAwait(false);
        if (targetCheck.IsFailure)
        {
            return Result.Failure<IReadOnlyList<ShiftAssignmentRuleDto>>(
                targetCheck.ErrorCode!,
                targetCheck.ErrorMessage!);
        }

        foreach (var targetId in request.TargetIds.Distinct())
        {
            var overlap = await HasOverlapAsync(
                    targetType,
                    targetId,
                    request.EffectiveFrom,
                    request.EffectiveTo,
                    excludeId: null,
                    cancellationToken)
                .ConfigureAwait(false);
            if (overlap)
            {
                return Result.Failure<IReadOnlyList<ShiftAssignmentRuleDto>>(
                    ErrorCodes.AttendanceShiftRuleOverlap,
                    ErrorCodes.AttendanceShiftRuleOverlap);
            }
        }

        var created = new List<ShiftAssignmentRule>();
        foreach (var targetId in request.TargetIds.Distinct())
        {
            var rule = ShiftAssignmentRule.Create(
                tenantContext.TenantId.Value,
                targetType,
                targetId,
                request.ShiftId,
                request.IsDayOff,
                request.EffectiveFrom,
                request.EffectiveTo,
                tenantContext.UserId);
            db.ShiftAssignmentRules.Add(rule);
            created.Add(rule);
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await audit.WriteAsync(
                tenantContext.TenantId.Value,
                tenantContext.UserId,
                AuditActions.AttendanceShiftRuleCreate,
                "ShiftAssignmentRule",
                null,
                null,
                JsonSerializer.Serialize(new
                {
                    targetType = targetType.ToString(),
                    count = created.Count,
                    request.ShiftId,
                    request.IsDayOff,
                    request.EffectiveFrom,
                    request.EffectiveTo,
                    backdated = request.EffectiveFrom < today,
                    note = "Historical AttendanceDaySummary rows are not recalculated.",
                }),
                cancellationToken)
            .ConfigureAwait(false);

        var dtos = await MapManyAsync(created, cancellationToken).ConfigureAwait(false);
        return Result.Success((IReadOnlyList<ShiftAssignmentRuleDto>)dtos);
    }

    public async Task<Result<ShiftAssignmentRuleDto>> UpdateAsync(
        Guid id,
        UpdateShiftAssignmentRuleRequest request,
        CancellationToken cancellationToken = default)
    {
        var writable = await subscriptionGuard.EnsureWritableAsync(cancellationToken).ConfigureAwait(false);
        if (writable.IsFailure)
        {
            return Result.Failure<ShiftAssignmentRuleDto>(writable.ErrorCode!, writable.ErrorMessage!);
        }

        var gate = await subscriptionGuard.EnsureFeatureAsync(FeatureKeys.Attendance, cancellationToken)
            .ConfigureAwait(false);
        if (gate.IsFailure)
        {
            return Result.Failure<ShiftAssignmentRuleDto>(gate.ErrorCode!, gate.ErrorMessage!);
        }

        if (tenantContext.TenantId is null || tenantContext.UserId is null)
        {
            return Result.Failure<ShiftAssignmentRuleDto>(ErrorCodes.TenantNotFound, ErrorCodes.TenantNotFound);
        }

        var rule = await db.ShiftAssignmentRules
            .SingleOrDefaultAsync(r => r.Id == id, cancellationToken)
            .ConfigureAwait(false);
        if (rule is null)
        {
            return Result.Failure<ShiftAssignmentRuleDto>(
                ErrorCodes.AttendanceShiftRuleNotFound,
                ErrorCodes.AttendanceShiftRuleNotFound);
        }

        if (!request.IsDayOff && request.ShiftId is null)
        {
            return Result.Failure<ShiftAssignmentRuleDto>(
                ErrorCodes.AttendanceShiftRuleValidationFailed,
                ErrorCodes.AttendanceShiftRuleValidationFailed);
        }

        var today = await TodayTenantAsync(cancellationToken).ConfigureAwait(false);
        if (request.EffectiveFrom < today && !request.ConfirmRecalc)
        {
            return Result.Failure<ShiftAssignmentRuleDto>(
                ErrorCodes.AttendanceShiftRuleBackdateRequiresConfirm,
                ErrorCodes.AttendanceShiftRuleBackdateRequiresConfirm);
        }

        var overlap = await HasOverlapAsync(
                rule.TargetType,
                rule.TargetId,
                request.EffectiveFrom,
                request.EffectiveTo,
                rule.Id,
                cancellationToken)
            .ConfigureAwait(false);
        if (overlap)
        {
            return Result.Failure<ShiftAssignmentRuleDto>(
                ErrorCodes.AttendanceShiftRuleOverlap,
                ErrorCodes.AttendanceShiftRuleOverlap);
        }

        rule.Update(request.ShiftId, request.IsDayOff, request.EffectiveFrom, request.EffectiveTo, tenantContext.UserId);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await audit.WriteAsync(
                tenantContext.TenantId.Value,
                tenantContext.UserId,
                AuditActions.AttendanceShiftRuleUpdate,
                "ShiftAssignmentRule",
                rule.Id,
                null,
                JsonSerializer.Serialize(new { rule.EffectiveFrom, rule.EffectiveTo, rule.ShiftId, rule.IsDayOff }),
                cancellationToken)
            .ConfigureAwait(false);

        var dto = (await MapManyAsync([rule], cancellationToken).ConfigureAwait(false))[0];
        return Result.Success(dto);
    }

    public async Task<Result<ShiftAssignmentRuleDto>> EndAsync(
        Guid id,
        EndShiftAssignmentRuleRequest request,
        CancellationToken cancellationToken = default)
    {
        var writable = await subscriptionGuard.EnsureWritableAsync(cancellationToken).ConfigureAwait(false);
        if (writable.IsFailure)
        {
            return Result.Failure<ShiftAssignmentRuleDto>(writable.ErrorCode!, writable.ErrorMessage!);
        }

        var gate = await subscriptionGuard.EnsureFeatureAsync(FeatureKeys.Attendance, cancellationToken)
            .ConfigureAwait(false);
        if (gate.IsFailure)
        {
            return Result.Failure<ShiftAssignmentRuleDto>(gate.ErrorCode!, gate.ErrorMessage!);
        }

        if (tenantContext.TenantId is null || tenantContext.UserId is null)
        {
            return Result.Failure<ShiftAssignmentRuleDto>(ErrorCodes.TenantNotFound, ErrorCodes.TenantNotFound);
        }

        var rule = await db.ShiftAssignmentRules
            .SingleOrDefaultAsync(r => r.Id == id, cancellationToken)
            .ConfigureAwait(false);
        if (rule is null)
        {
            return Result.Failure<ShiftAssignmentRuleDto>(
                ErrorCodes.AttendanceShiftRuleNotFound,
                ErrorCodes.AttendanceShiftRuleNotFound);
        }

        try
        {
            rule.End(request.EffectiveTo, tenantContext.UserId);
        }
        catch (ArgumentException)
        {
            return Result.Failure<ShiftAssignmentRuleDto>(
                ErrorCodes.AttendanceShiftRuleValidationFailed,
                ErrorCodes.AttendanceShiftRuleValidationFailed);
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await audit.WriteAsync(
                tenantContext.TenantId.Value,
                tenantContext.UserId,
                AuditActions.AttendanceShiftRuleEnd,
                "ShiftAssignmentRule",
                rule.Id,
                null,
                JsonSerializer.Serialize(new { request.EffectiveTo }),
                cancellationToken)
            .ConfigureAwait(false);

        var dto = (await MapManyAsync([rule], cancellationToken).ConfigureAwait(false))[0];
        return Result.Success(dto);
    }

    public async Task<Result<PreviewShiftAssignmentRulesDto>> PreviewAsync(
        PreviewShiftAssignmentRulesRequest request,
        CancellationToken cancellationToken = default)
    {
        var gate = await subscriptionGuard.EnsureFeatureAsync(FeatureKeys.Attendance, cancellationToken)
            .ConfigureAwait(false);
        if (gate.IsFailure)
        {
            return Result.Failure<PreviewShiftAssignmentRulesDto>(gate.ErrorCode!, gate.ErrorMessage!);
        }

        if (!Enum.TryParse<ShiftAssignmentTargetType>(request.TargetType, true, out var targetType)
            || request.TargetIds is null
            || request.TargetIds.Count == 0)
        {
            return Result.Failure<PreviewShiftAssignmentRulesDto>(
                ErrorCodes.AttendanceShiftRuleValidationFailed,
                ErrorCodes.AttendanceShiftRuleValidationFailed);
        }

        var employeeQuery = db.Employees.AsNoTracking()
            .Where(e =>
                e.Status == EmployeeStatus.Active
                || e.Status == EmployeeStatus.Probation
                || e.Status == EmployeeStatus.OnLeave);

        employeeQuery = targetType switch
        {
            ShiftAssignmentTargetType.Employee => employeeQuery.Where(e => request.TargetIds.Contains(e.Id)),
            ShiftAssignmentTargetType.JobTitle => employeeQuery.Where(e =>
                e.JobTitleId != null && request.TargetIds.Contains(e.JobTitleId.Value)),
            ShiftAssignmentTargetType.Department => employeeQuery.Where(e =>
                e.DepartmentId != null && request.TargetIds.Contains(e.DepartmentId.Value)),
            ShiftAssignmentTargetType.Branch => employeeQuery.Where(e => request.TargetIds.Contains(e.BranchId)),
            _ => employeeQuery.Where(_ => false),
        };

        var empIds = await employeeQuery.Select(e => e.Id).ToListAsync(cancellationToken).ConfigureAwait(false);
        var affected = empIds.Count;

        var from = request.EffectiveFrom;
        var to = request.EffectiveTo ?? request.EffectiveFrom.AddYears(1);
        var overrideCount = await db.ShiftAssignments.AsNoTracking()
            .Where(a => empIds.Contains(a.EmployeeId) && a.WorkDate >= from && a.WorkDate <= to)
            .Select(a => a.EmployeeId)
            .Distinct()
            .CountAsync(cancellationToken)
            .ConfigureAwait(false);

        return Result.Success(new PreviewShiftAssignmentRulesDto(
            affected,
            overrideCount,
            "Day-level assignments keep winning over this rule. Historical day summaries are not recalculated."));
    }

    private async Task<bool> HasOverlapAsync(
        ShiftAssignmentTargetType targetType,
        Guid targetId,
        DateOnly from,
        DateOnly? to,
        Guid? excludeId,
        CancellationToken cancellationToken)
    {
        var end = to ?? DateOnly.MaxValue;
        var q = db.ShiftAssignmentRules.AsNoTracking()
            .Where(r =>
                r.TargetType == targetType
                && r.TargetId == targetId
                && r.EffectiveFrom <= end
                && (r.EffectiveTo == null || r.EffectiveTo >= from));
        if (excludeId is Guid eid)
        {
            q = q.Where(r => r.Id != eid);
        }

        return await q.AnyAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result> TargetsExistAsync(
        ShiftAssignmentTargetType type,
        IReadOnlyList<Guid> ids,
        CancellationToken cancellationToken)
    {
        var distinct = ids.Distinct().ToList();
        var count = type switch
        {
            ShiftAssignmentTargetType.Employee => await db.Employees.AsNoTracking()
                .CountAsync(e => distinct.Contains(e.Id), cancellationToken).ConfigureAwait(false),
            ShiftAssignmentTargetType.JobTitle => await db.JobTitles.AsNoTracking()
                .CountAsync(j => distinct.Contains(j.Id) && j.IsActive, cancellationToken).ConfigureAwait(false),
            ShiftAssignmentTargetType.Department => await db.Departments.AsNoTracking()
                .CountAsync(d => distinct.Contains(d.Id) && d.IsActive, cancellationToken).ConfigureAwait(false),
            ShiftAssignmentTargetType.Branch => await db.Branches.AsNoTracking()
                .CountAsync(b => distinct.Contains(b.Id) && b.IsActive, cancellationToken).ConfigureAwait(false),
            _ => 0,
        };

        return count == distinct.Count
            ? Result.Success()
            : Result.Failure(ErrorCodes.AttendanceShiftRuleValidationFailed, ErrorCodes.AttendanceShiftRuleValidationFailed);
    }

    private async Task<DateOnly> TodayTenantAsync(CancellationToken cancellationToken)
    {
        var tz = await db.Tenants.AsNoTracking()
            .Where(t => t.Id == tenantContext.TenantId!.Value)
            .Select(t => t.TimeZone)
            .SingleAsync(cancellationToken)
            .ConfigureAwait(false);
        return TenantTimeZones.ToWorkDate(DateTimeOffset.UtcNow, tz);
    }

    private async Task<List<ShiftAssignmentRuleDto>> MapManyAsync(
        IReadOnlyList<ShiftAssignmentRule> rows,
        CancellationToken cancellationToken)
    {
        if (rows.Count == 0)
        {
            return [];
        }

        var shiftIds = rows.Where(r => r.ShiftId is not null).Select(r => r.ShiftId!.Value).Distinct().ToList();
        var shifts = await db.Shifts.AsNoTracking()
            .Where(s => shiftIds.Contains(s.Id))
            .Select(s => new { s.Id, s.NameAr, s.NameEn })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var shiftMap = shifts.ToDictionary(s => s.Id);

        var empIds = rows.Where(r => r.TargetType == ShiftAssignmentTargetType.Employee).Select(r => r.TargetId).ToList();
        var deptIds = rows.Where(r => r.TargetType == ShiftAssignmentTargetType.Department).Select(r => r.TargetId).ToList();
        var branchIds = rows.Where(r => r.TargetType == ShiftAssignmentTargetType.Branch).Select(r => r.TargetId).ToList();
        var jobIds = rows.Where(r => r.TargetType == ShiftAssignmentTargetType.JobTitle).Select(r => r.TargetId).ToList();

        var empNames = await db.Employees.AsNoTracking()
            .Where(e => empIds.Contains(e.Id))
            .Select(e => new { e.Id, e.FullNameAr })
            .ToDictionaryAsync(e => e.Id, e => e.FullNameAr, cancellationToken)
            .ConfigureAwait(false);
        var deptNames = await db.Departments.AsNoTracking()
            .Where(d => deptIds.Contains(d.Id))
            .Select(d => new { d.Id, d.NameAr })
            .ToDictionaryAsync(d => d.Id, d => d.NameAr, cancellationToken)
            .ConfigureAwait(false);
        var branchNames = await db.Branches.AsNoTracking()
            .Where(b => branchIds.Contains(b.Id))
            .Select(b => new { b.Id, b.NameAr })
            .ToDictionaryAsync(b => b.Id, b => b.NameAr, cancellationToken)
            .ConfigureAwait(false);
        var jobNames = await db.JobTitles.AsNoTracking()
            .Where(j => jobIds.Contains(j.Id))
            .Select(j => new { j.Id, j.NameAr })
            .ToDictionaryAsync(j => j.Id, j => j.NameAr, cancellationToken)
            .ConfigureAwait(false);

        string? NameFor(ShiftAssignmentRule r) => r.TargetType switch
        {
            ShiftAssignmentTargetType.Employee => empNames.GetValueOrDefault(r.TargetId),
            ShiftAssignmentTargetType.Department => deptNames.GetValueOrDefault(r.TargetId),
            ShiftAssignmentTargetType.Branch => branchNames.GetValueOrDefault(r.TargetId),
            ShiftAssignmentTargetType.JobTitle => jobNames.GetValueOrDefault(r.TargetId),
            _ => null,
        };

        return rows.Select(r =>
        {
            shiftMap.TryGetValue(r.ShiftId ?? Guid.Empty, out var sh);
            return new ShiftAssignmentRuleDto(
                r.Id,
                r.TargetType.ToString(),
                r.TargetId,
                NameFor(r),
                r.ShiftId,
                sh?.NameAr,
                sh?.NameEn,
                r.IsDayOff,
                r.EffectiveFrom,
                r.EffectiveTo);
        }).ToList();
    }
}
