using Kadr.Platform.Domain.Attendance;
using Kadr.Platform.Domain.Employees;
using Microsoft.EntityFrameworkCore;

namespace Kadr.Infrastructure.Persistence.Attendance;

internal readonly record struct EffectiveShift(Guid? ShiftId, bool IsDayOff, string Source);

/// <summary>
/// Resolves effective shift for an employee on a work date:
/// day override &gt; employee rule &gt; job title &gt; department &gt; branch &gt; default.
/// </summary>
internal static class ShiftRuleResolver
{
    public static async Task<EffectiveShift> ResolveAsync(
        KadrDbContext db,
        Guid employeeId,
        DateOnly workDate,
        Guid? defaultShiftId,
        CancellationToken cancellationToken)
    {
        var day = await db.ShiftAssignments.AsNoTracking()
            .Where(a => a.EmployeeId == employeeId && a.WorkDate == workDate)
            .Select(a => new { a.ShiftId, a.IsDayOff })
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (day is not null)
        {
            return new EffectiveShift(day.IsDayOff ? null : day.ShiftId, day.IsDayOff, "day");
        }

        var emp = await db.Employees.AsNoTracking()
            .Where(e => e.Id == employeeId)
            .Select(e => new { e.Id, e.JobTitleId, e.DepartmentId, e.BranchId })
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (emp is null)
        {
            return new EffectiveShift(defaultShiftId, false, "default");
        }

        var candidateTargetIds = new List<(ShiftAssignmentTargetType Type, Guid Id)>();
        candidateTargetIds.Add((ShiftAssignmentTargetType.Employee, emp.Id));
        if (emp.JobTitleId is Guid jt)
        {
            candidateTargetIds.Add((ShiftAssignmentTargetType.JobTitle, jt));
        }

        if (emp.DepartmentId is Guid dep)
        {
            candidateTargetIds.Add((ShiftAssignmentTargetType.Department, dep));
        }

        candidateTargetIds.Add((ShiftAssignmentTargetType.Branch, emp.BranchId));

        var typeIds = candidateTargetIds.Select(c => c.Id).ToList();
        var rules = await db.ShiftAssignmentRules.AsNoTracking()
            .Where(r =>
                typeIds.Contains(r.TargetId)
                && r.EffectiveFrom <= workDate
                && (r.EffectiveTo == null || r.EffectiveTo >= workDate))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        ShiftAssignmentRule? best = null;
        var bestRank = int.MaxValue;
        foreach (var (type, id) in candidateTargetIds)
        {
            var match = rules.FirstOrDefault(r => r.TargetType == type && r.TargetId == id);
            if (match is null)
            {
                continue;
            }

            var rank = ShiftAssignmentRule.PrecedenceRank(type);
            if (rank < bestRank)
            {
                best = match;
                bestRank = rank;
            }
        }

        if (best is not null)
        {
            return new EffectiveShift(
                best.IsDayOff ? null : best.ShiftId,
                best.IsDayOff,
                best.TargetType.ToString());
        }

        return new EffectiveShift(defaultShiftId, false, "default");
    }

    /// <summary>
    /// Batch resolve for many employees on one date (set-based; used by board/tests).
    /// </summary>
    public static async Task<IReadOnlyDictionary<Guid, EffectiveShift>> ResolveManyAsync(
        KadrDbContext db,
        IReadOnlyList<Guid> employeeIds,
        DateOnly workDate,
        Guid? defaultShiftId,
        CancellationToken cancellationToken)
    {
        if (employeeIds.Count == 0)
        {
            return new Dictionary<Guid, EffectiveShift>();
        }

        var days = await db.ShiftAssignments.AsNoTracking()
            .Where(a => employeeIds.Contains(a.EmployeeId) && a.WorkDate == workDate)
            .Select(a => new { a.EmployeeId, a.ShiftId, a.IsDayOff })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var dayByEmp = days.ToDictionary(d => d.EmployeeId);

        var emps = await db.Employees.AsNoTracking()
            .Where(e => employeeIds.Contains(e.Id))
            .Select(e => new { e.Id, e.JobTitleId, e.DepartmentId, e.BranchId })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var targetIds = new HashSet<Guid>();
        foreach (var e in emps)
        {
            targetIds.Add(e.Id);
            if (e.JobTitleId is Guid jt)
            {
                targetIds.Add(jt);
            }

            if (e.DepartmentId is Guid dep)
            {
                targetIds.Add(dep);
            }

            targetIds.Add(e.BranchId);
        }

        var rules = await db.ShiftAssignmentRules.AsNoTracking()
            .Where(r =>
                targetIds.Contains(r.TargetId)
                && r.EffectiveFrom <= workDate
                && (r.EffectiveTo == null || r.EffectiveTo >= workDate))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var result = new Dictionary<Guid, EffectiveShift>(employeeIds.Count);
        foreach (var e in emps)
        {
            if (dayByEmp.TryGetValue(e.Id, out var day))
            {
                result[e.Id] = new EffectiveShift(day.IsDayOff ? null : day.ShiftId, day.IsDayOff, "day");
                continue;
            }

            ShiftAssignmentRule? best = null;
            var bestRank = int.MaxValue;
            void Consider(ShiftAssignmentTargetType type, Guid id)
            {
                var match = rules.FirstOrDefault(r => r.TargetType == type && r.TargetId == id);
                if (match is null)
                {
                    return;
                }

                var rank = ShiftAssignmentRule.PrecedenceRank(type);
                if (rank < bestRank)
                {
                    best = match;
                    bestRank = rank;
                }
            }

            Consider(ShiftAssignmentTargetType.Employee, e.Id);
            if (e.JobTitleId is Guid jt)
            {
                Consider(ShiftAssignmentTargetType.JobTitle, jt);
            }

            if (e.DepartmentId is Guid dep)
            {
                Consider(ShiftAssignmentTargetType.Department, dep);
            }

            Consider(ShiftAssignmentTargetType.Branch, e.BranchId);

            result[e.Id] = best is not null
                ? new EffectiveShift(best.IsDayOff ? null : best.ShiftId, best.IsDayOff, best.TargetType.ToString())
                : new EffectiveShift(defaultShiftId, false, "default");
        }

        foreach (var id in employeeIds)
        {
            result.TryAdd(id, new EffectiveShift(defaultShiftId, false, "default"));
        }

        return result;
    }
}
