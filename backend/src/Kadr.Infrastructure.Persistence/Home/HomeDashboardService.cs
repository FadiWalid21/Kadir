using Kadr.Identity.Application.Auth;
using Kadr.Platform.Application.Abstractions;
using Kadr.Platform.Application.Home;
using Kadr.Platform.Domain.Attendance;
using Kadr.Platform.Domain.Employees;
using Kadr.Platform.Domain.Leave;
using Kadr.Platform.Domain.Workflows;
using Kadr.SharedKernel.Constants;
using Kadr.SharedKernel.Results;
using Kadr.SharedKernel.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Kadr.Infrastructure.Persistence.Home;

public sealed class HomeDashboardService(
    KadrDbContext db,
    ITenantContext tenantContext,
    IPermissionService permissions,
    IAttendanceService attendance) : IHomeDashboardService
{
    private const int TopN = 5;

    public async Task<Result<HomeDashboardDto>> GetDashboardAsync(CancellationToken cancellationToken = default)
    {
        if (tenantContext.TenantId is null || tenantContext.UserId is null)
        {
            return Result.Failure<HomeDashboardDto>(ErrorCodes.TenantNotFound, ErrorCodes.TenantNotFound);
        }

        var tenantId = tenantContext.TenantId.Value;
        var userId = tenantContext.UserId.Value;
        var perms = await permissions.GetPermissionsAsync(tenantId, userId, cancellationToken).ConfigureAwait(false);
        var permSet = perms.ToHashSet(StringComparer.Ordinal);

        var userName = await db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => u.FullName)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false) ?? string.Empty;

        var tenant = await db.Tenants.AsNoTracking()
            .Where(t => t.Id == tenantId)
            .Select(t => new { t.Name, t.LogoFileId })
            .SingleAsync(cancellationToken)
            .ConfigureAwait(false);

        var greeting = new HomeGreetingDto(
            userName,
            tenant.Name,
            tenant.LogoFileId is null ? null : $"/api/v1/files/{tenant.LogoFileId:D}");

        var checklist = await BuildChecklistAsync(userId, tenant.LogoFileId, cancellationToken).ConfigureAwait(false);

        HomeEmployeeSectionDto? employee = null;
        if (permSet.Contains(PermissionCodes.AttendancePunch)
            || permSet.Contains(PermissionCodes.LeaveReadOwn))
        {
            employee = await BuildEmployeeAsync(userId, permSet, cancellationToken).ConfigureAwait(false);
        }

        HomeManagerSectionDto? manager = null;
        if (permSet.Contains(PermissionCodes.LeaveApprove)
            || permSet.Contains(PermissionCodes.AttendanceReadTeam))
        {
            manager = await BuildManagerAsync(permSet, cancellationToken).ConfigureAwait(false);
        }

        HomeHrSectionDto? hr = null;
        if (permSet.Contains(PermissionCodes.EmployeesRead))
        {
            hr = await BuildHrAsync(cancellationToken).ConfigureAwait(false);
        }

        return Result.Success(new HomeDashboardDto(greeting, checklist, employee, manager, hr));
    }

    private async Task<HomeChecklistDto?> BuildChecklistAsync(
        Guid userId,
        Guid? logoFileId,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var logoDone = logoFileId is not null;
        var inviteDone = await db.Users.AsNoTracking().AnyAsync(u => u.Id != userId, cancellationToken)
                .ConfigureAwait(false)
            || await db.Invitations.AsNoTracking()
                .AnyAsync(
                    i => i.AcceptedAt == null && i.RevokedAt == null && i.ExpiresAt > now,
                    cancellationToken)
                .ConfigureAwait(false);
        var employeeDone = await db.Employees.AsNoTracking().AnyAsync(cancellationToken).ConfigureAwait(false);
        var shiftsDone = await db.Shifts.AsNoTracking().AnyAsync(cancellationToken).ConfigureAwait(false);

        var items = new List<HomeChecklistItemDto>
        {
            new("logo", logoDone, "/settings/company"),
            new("invite", inviteDone, "/settings/users"),
            new("employee", employeeDone, "/employees"),
            new("shifts", shiftsDone, "/settings/templates"),
        };
        var done = items.Count(i => i.Done);
        if (done >= items.Count)
        {
            return null;
        }

        return new HomeChecklistDto(done, items.Count, IsComplete: false, items);
    }

    private async Task<HomeEmployeeSectionDto> BuildEmployeeAsync(
        Guid userId,
        HashSet<string> permSet,
        CancellationToken cancellationToken)
    {
        HomePunchSummaryDto? punch = null;
        if (permSet.Contains(PermissionCodes.AttendancePunch))
        {
            var today = await attendance.GetMyTodayAsync(cancellationToken).ConfigureAwait(false);
            if (today.IsSuccess && today.Value is not null)
            {
                var t = today.Value;
                var next = t.OpenPunch is null ? "in" : "out";
                punch = new HomePunchSummaryDto(
                    t.CanPunch,
                    next,
                    t.OpenPunch?.PunchedAt ?? t.Punches.LastOrDefault()?.PunchedAt,
                    t.DayStatus);
            }
        }

        IReadOnlyList<HomeBalanceSummaryDto> balances = [];
        if (permSet.Contains(PermissionCodes.LeaveReadOwn))
        {
            var year = DateTimeOffset.UtcNow.Year;
            var employeeId = await db.Employees.AsNoTracking()
                .Where(e => e.UserId == userId)
                .Select(e => (Guid?)e.Id)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);
            if (employeeId is Guid eid)
            {
                balances = await (
                        from b in db.LeaveBalances.AsNoTracking()
                        join t in db.LeaveTypes.AsNoTracking() on b.LeaveTypeId equals t.Id
                        where b.EmployeeId == eid && b.Year == year
                        orderby t.NameAr
                        select new HomeBalanceSummaryDto(
                            t.Id,
                            t.NameAr,
                            t.NameEn ?? t.NameAr,
                            b.Entitled + b.Accrued + b.CarriedIn + b.Adjustment - b.Used,
                            b.Entitled))
                    .Take(TopN)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                var pending = await db.LeaveRequests.AsNoTracking()
                    .Where(r => r.EmployeeId == eid && r.Status == LeaveRequestStatus.Pending)
                    .OrderByDescending(r => r.CreatedAt)
                    .Take(TopN)
                    .Join(
                        db.LeaveTypes.AsNoTracking(),
                        r => r.LeaveTypeId,
                        t => t.Id,
                        (r, t) => new HomePendingRequestDto(
                            r.Id,
                            "leave",
                            r.Status.ToString(),
                            r.StartDate,
                            r.EndDate,
                            t.NameAr,
                            t.NameEn))
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                return new HomeEmployeeSectionDto(punch, balances, pending);
            }
        }

        return new HomeEmployeeSectionDto(punch, balances, []);
    }

    private async Task<HomeManagerSectionDto> BuildManagerAsync(
        HashSet<string> permSet,
        CancellationToken cancellationToken)
    {
        var pendingQuery = db.WorkflowTasks.AsNoTracking().Where(t => t.IsOpen);
        var pendingCount = await pendingQuery.CountAsync(cancellationToken).ConfigureAwait(false);
        var approvals = await (
                from t in pendingQuery
                join i in db.WorkflowInstances.AsNoTracking() on t.InstanceId equals i.Id
                join e in db.Employees.AsNoTracking() on i.RequesterEmployeeId equals e.Id
                orderby t.CreatedAt descending
                select new HomeApprovalItemDto(
                    t.Id,
                    i.RequestType.ToString(),
                    e.FullNameAr,
                    e.FullNameEn,
                    t.CreatedAt))
            .Take(TopN)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var team = new HomeTeamAttendanceDto(0, 0, 0, 0);
        if (permSet.Contains(PermissionCodes.AttendanceReadTeam)
            || permSet.Contains(PermissionCodes.AttendanceReadAll))
        {
            var board = await attendance.GetBoardTodayAsync(null, null, null, null, null, 1, 1, cancellationToken)
                .ConfigureAwait(false);
            if (board.IsSuccess && board.Value is not null)
            {
                var s = board.Value.Stats;
                team = new HomeTeamAttendanceDto(s.Present, s.Late, s.Absent, s.OnLeave);
            }
        }

        return new HomeManagerSectionDto(pendingCount, approvals, team);
    }

    private async Task<HomeHrSectionDto> BuildHrAsync(CancellationToken cancellationToken)
    {
        var headcount = await db.Employees.AsNoTracking()
            .CountAsync(
                e => e.Status == EmployeeStatus.Active
                     || e.Status == EmployeeStatus.Probation
                     || e.Status == EmployeeStatus.OnLeave,
                cancellationToken)
            .ConfigureAwait(false);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var late = await db.AttendanceDaySummaries.AsNoTracking()
            .CountAsync(s => s.WorkDate == today && s.Status == AttendanceDayStatus.Late, cancellationToken)
            .ConfigureAwait(false);
        var absent = await db.AttendanceDaySummaries.AsNoTracking()
            .CountAsync(s => s.WorkDate == today && s.Status == AttendanceDayStatus.Absent, cancellationToken)
            .ConfigureAwait(false);
        var pending = await db.WorkflowTasks.AsNoTracking()
            .CountAsync(t => t.IsOpen, cancellationToken)
            .ConfigureAwait(false);

        var horizon = today.AddDays(30);
        var expiring = await (
                from d in db.EmployeeDocuments.AsNoTracking()
                join e in db.Employees.AsNoTracking() on d.EmployeeId equals e.Id
                join t in db.DocumentTypes.AsNoTracking() on d.DocumentTypeId equals t.Id
                where d.ExpiresOn != null && d.ExpiresOn <= horizon
                orderby d.ExpiresOn
                select new HomeExpiringDocumentDto(
                    d.Id,
                    e.Id,
                    e.FullNameAr,
                    e.FullNameEn,
                    t.NameAr,
                    t.NameEn,
                    d.ExpiresOn))
            .Take(TopN)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new HomeHrSectionDto(headcount, late, absent, pending, expiring);
    }
}
