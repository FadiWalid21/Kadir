using Kadr.Platform.Application.Attendance;
using Kadr.Platform.Application.Leave;
using Kadr.Platform.Domain.Attendance;
using Kadr.Platform.Domain.Workflows;
using Kadr.SharedKernel.Constants;
using Kadr.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Kadr.Infrastructure.Persistence.Attendance;

public sealed partial class AttendanceService
{
    private static readonly HashSet<string> ValidProblemTypes =
    [
        "ForgotIn",
        "ForgotOut",
        "WrongTime",
        "Other",
    ];

    public async Task<Result<AttendanceCorrectionRequestDto>> CreateCorrectionAsync(
        CreateAttendanceCorrectionRequest request,
        CancellationToken cancellationToken = default)
    {
        var writable = await EnsureWritableFeatureAsync(cancellationToken).ConfigureAwait(false);
        if (writable.IsFailure)
        {
            return Result.Failure<AttendanceCorrectionRequestDto>(writable.ErrorCode!, writable.ErrorMessage!);
        }

        if (tenantContext.TenantId is null || tenantContext.UserId is null)
        {
            return Result.Failure<AttendanceCorrectionRequestDto>(
                ErrorCodes.TenantNotFound,
                ErrorCodes.TenantNotFound);
        }

        if (!ValidProblemTypes.Contains(request.ProblemType)
            || string.IsNullOrWhiteSpace(request.Reason)
            || request.Reason.Trim().Length < 5)
        {
            return Result.Failure<AttendanceCorrectionRequestDto>(
                ErrorCodes.AttendanceCorrectionInvalid,
                ErrorCodes.AttendanceCorrectionInvalid);
        }

        var linked = await ResolveLinkedEmployeeAsync(cancellationToken).ConfigureAwait(false);
        if (linked.IsFailure)
        {
            return Result.Failure<AttendanceCorrectionRequestDto>(linked.ErrorCode!, linked.ErrorMessage!);
        }

        var (employee, _, tenant) = linked.Value!;
        var today = TenantTimeZones.ToWorkDate(DateTimeOffset.UtcNow, tenant.TimeZone);
        if (request.WorkDate > today || request.WorkDate < today.AddDays(-30))
        {
            return Result.Failure<AttendanceCorrectionRequestDto>(
                ErrorCodes.AttendanceCorrectionInvalid,
                ErrorCodes.AttendanceCorrectionInvalid);
        }

        var monthStart = new DateOnly(request.WorkDate.Year, request.WorkDate.Month, 1);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);
        var cap = await ReadIntSettingAsync(SettingKeys.AttendanceCorrectionMonthlyCap, 5, cancellationToken)
            .ConfigureAwait(false);
        var used = await db.AttendanceCorrectionRequests.AsNoTracking()
            .CountAsync(
                c => c.EmployeeId == employee.Id
                     && c.WorkDate >= monthStart
                     && c.WorkDate <= monthEnd
                     && c.Status != AttendanceCorrectionStatus.Cancelled,
                cancellationToken)
            .ConfigureAwait(false);
        if (used >= cap)
        {
            return Result.Failure<AttendanceCorrectionRequestDto>(
                ErrorCodes.AttendanceCorrectionCap,
                ErrorCodes.AttendanceCorrectionCap);
        }

        var entity = AttendanceCorrectionRequest.Create(
            tenant.Id,
            employee.Id,
            tenantContext.UserId.Value,
            request.WorkDate,
            request.ProblemType,
            request.ProposedTime,
            request.Reason,
            request.AttachmentFileId);
        db.AttendanceCorrectionRequests.Add(entity);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var wf = await workflowEngine.StartAsync(
                WorkflowRequestType.AttendanceCorrection,
                entity.Id,
                employee.Id,
                cancellationToken)
            .ConfigureAwait(false);
        if (wf.IsFailure)
        {
            return Result.Failure<AttendanceCorrectionRequestDto>(wf.ErrorCode!, wf.ErrorMessage!);
        }

        await audit.WriteAsync(
                tenant.Id,
                tenantContext.UserId,
                "attendance.correction.create",
                nameof(AttendanceCorrectionRequest),
                entity.Id,
                null,
                null,
                cancellationToken)
            .ConfigureAwait(false);

        return Result.Success(await MapCorrectionAsync(entity, cancellationToken).ConfigureAwait(false));
    }

    public async Task<Result<PagedAttendanceCorrectionsDto>> ListMyCorrectionsAsync(
        string? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var gate = await EnsureFeatureAsync(cancellationToken).ConfigureAwait(false);
        if (gate.IsFailure)
        {
            return Result.Failure<PagedAttendanceCorrectionsDto>(gate.ErrorCode!, gate.ErrorMessage!);
        }

        var linked = await ResolveLinkedEmployeeAsync(cancellationToken).ConfigureAwait(false);
        if (linked.IsFailure)
        {
            return Result.Failure<PagedAttendanceCorrectionsDto>(linked.ErrorCode!, linked.ErrorMessage!);
        }

        page = page < 1 ? 1 : page;
        pageSize = pageSize < 1 ? 20 : Math.Min(pageSize, MaxPageSize);
        var empId = linked.Value!.Employee.Id;

        var query = db.AttendanceCorrectionRequests.AsNoTracking()
            .Where(c => c.EmployeeId == empId);
        if (!string.IsNullOrWhiteSpace(status)
            && Enum.TryParse<AttendanceCorrectionStatus>(status, true, out var st))
        {
            query = query.Where(c => c.Status == st);
        }

        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var pending = await db.AttendanceCorrectionRequests.AsNoTracking()
            .CountAsync(
                c => c.EmployeeId == empId && c.Status == AttendanceCorrectionStatus.Pending,
                cancellationToken)
            .ConfigureAwait(false);
        var rows = await query
            .OrderByDescending(c => c.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var items = new List<AttendanceCorrectionRequestDto>();
        foreach (var row in rows)
        {
            items.Add(await MapCorrectionAsync(row, cancellationToken).ConfigureAwait(false));
        }

        return Result.Success(new PagedAttendanceCorrectionsDto(items, page, pageSize, total, pending));
    }

    public async Task<Result<PagedAttendanceCorrectionsDto>> ListCorrectionsInboxAsync(
        string? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var gate = await EnsureFeatureAsync(cancellationToken).ConfigureAwait(false);
        if (gate.IsFailure)
        {
            return Result.Failure<PagedAttendanceCorrectionsDto>(gate.ErrorCode!, gate.ErrorMessage!);
        }

        if (tenantContext.TenantId is null || tenantContext.UserId is null)
        {
            return Result.Failure<PagedAttendanceCorrectionsDto>(
                ErrorCodes.TenantNotFound,
                ErrorCodes.TenantNotFound);
        }

        page = page < 1 ? 1 : page;
        pageSize = pageSize < 1 ? 20 : Math.Min(pageSize, MaxPageSize);

        var scope = await Employees.EmployeeDataScope.ResolveAsync(
                db,
                tenantContext.UserId.Value,
                cancellationToken)
            .ConfigureAwait(false);
        var empQuery = Employees.EmployeeDataScope.Apply(
            db.Employees.AsNoTracking(),
            scope.Scope,
            tenantContext.UserId.Value,
            scope.LinkedEmployeeId,
            scope.DepartmentId,
            scope.BranchId);
        var empIds = await empQuery.Select(e => e.Id).ToListAsync(cancellationToken).ConfigureAwait(false);

        var query = db.AttendanceCorrectionRequests.AsNoTracking()
            .Where(c => empIds.Contains(c.EmployeeId));
        if (!string.IsNullOrWhiteSpace(status)
            && Enum.TryParse<AttendanceCorrectionStatus>(status, true, out var st))
        {
            query = query.Where(c => c.Status == st);
        }

        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var pending = await db.AttendanceCorrectionRequests.AsNoTracking()
            .CountAsync(
                c => empIds.Contains(c.EmployeeId) && c.Status == AttendanceCorrectionStatus.Pending,
                cancellationToken)
            .ConfigureAwait(false);
        var rows = await query
            .OrderByDescending(c => c.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var items = new List<AttendanceCorrectionRequestDto>();
        foreach (var row in rows)
        {
            items.Add(await MapCorrectionAsync(row, cancellationToken).ConfigureAwait(false));
        }

        return Result.Success(new PagedAttendanceCorrectionsDto(items, page, pageSize, total, pending));
    }

    public async Task<Result<AttendanceCorrectionRequestDto>> ApproveCorrectionAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var taskId = await FindOpenTaskIdAsync(WorkflowRequestType.AttendanceCorrection, id, cancellationToken)
            .ConfigureAwait(false);
        if (taskId is null)
        {
            return Result.Failure<AttendanceCorrectionRequestDto>(
                ErrorCodes.RequestsNotFound,
                ErrorCodes.RequestsNotFound);
        }

        var decided = await leaveService.ApproveTaskAsync(taskId.Value, cancellationToken).ConfigureAwait(false);
        if (decided.IsFailure)
        {
            return Result.Failure<AttendanceCorrectionRequestDto>(decided.ErrorCode!, decided.ErrorMessage!);
        }

        var entity = await db.AttendanceCorrectionRequests.AsNoTracking()
            .SingleAsync(c => c.Id == id, cancellationToken)
            .ConfigureAwait(false);
        return Result.Success(await MapCorrectionAsync(entity, cancellationToken).ConfigureAwait(false));
    }

    public async Task<Result<AttendanceCorrectionRequestDto>> RejectCorrectionAsync(
        Guid id,
        RejectAttendanceCorrectionRequest request,
        CancellationToken cancellationToken = default)
    {
        var taskId = await FindOpenTaskIdAsync(WorkflowRequestType.AttendanceCorrection, id, cancellationToken)
            .ConfigureAwait(false);
        if (taskId is null)
        {
            return Result.Failure<AttendanceCorrectionRequestDto>(
                ErrorCodes.RequestsNotFound,
                ErrorCodes.RequestsNotFound);
        }

        var decided = await leaveService
            .RejectTaskAsync(taskId.Value, new RejectRequestBody(request.Reason), cancellationToken)
            .ConfigureAwait(false);
        if (decided.IsFailure)
        {
            return Result.Failure<AttendanceCorrectionRequestDto>(decided.ErrorCode!, decided.ErrorMessage!);
        }

        var entity = await db.AttendanceCorrectionRequests.AsNoTracking()
            .SingleAsync(c => c.Id == id, cancellationToken)
            .ConfigureAwait(false);
        return Result.Success(await MapCorrectionAsync(entity, cancellationToken).ConfigureAwait(false));
    }

    private async Task<Guid?> FindOpenTaskIdAsync(
        WorkflowRequestType requestType,
        Guid subjectId,
        CancellationToken cancellationToken)
    {
        var instanceId = await db.WorkflowInstances.AsNoTracking()
            .Where(i => i.RequestType == requestType
                        && i.SubjectId == subjectId
                        && i.Status == WorkflowInstanceStatus.Pending)
            .OrderByDescending(i => i.CreatedAt)
            .Select(i => (Guid?)i.Id)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (instanceId is null)
        {
            return null;
        }

        return await db.WorkflowTasks.AsNoTracking()
            .Where(t => t.InstanceId == instanceId && t.IsOpen)
            .OrderBy(t => t.StepOrder)
            .Select(t => (Guid?)t.Id)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<AttendanceCorrectionRequestDto> MapCorrectionAsync(
        AttendanceCorrectionRequest entity,
        CancellationToken cancellationToken)
    {
        var emp = await db.Employees.AsNoTracking()
            .Where(e => e.Id == entity.EmployeeId)
            .Select(e => new { e.FullNameAr, e.FullNameEn })
            .SingleAsync(cancellationToken)
            .ConfigureAwait(false);

        string? decidedByName = null;
        if (entity.DecidedByUserId is Guid uid)
        {
            decidedByName = await db.Users.AsNoTracking()
                .Where(u => u.Id == uid)
                .Select(u => u.FullName)
                .SingleOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        return new AttendanceCorrectionRequestDto(
            entity.Id,
            entity.EmployeeId,
            emp.FullNameAr,
            emp.FullNameEn ?? string.Empty,
            entity.WorkDate,
            entity.ProblemType,
            entity.ProposedTime,
            entity.Reason,
            entity.AttachmentFileId,
            entity.Status.ToString(),
            entity.RejectReason,
            entity.CreatedAt,
            entity.DecidedAt,
            decidedByName);
    }

}
