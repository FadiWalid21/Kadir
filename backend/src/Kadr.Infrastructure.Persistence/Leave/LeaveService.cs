using System.Security.Cryptography;
using System.Text;
using Kadr.Platform.Application.Abstractions;
using Kadr.Platform.Application.Leave;
using Kadr.Platform.Domain.Attendance;
using Kadr.Platform.Domain.Employees;
using Kadr.Platform.Domain.Files;
using Kadr.Platform.Domain.Leave;
using Kadr.Platform.Domain.Templates;
using Kadr.Platform.Domain.Workflows;
using Kadr.SharedKernel.Abstractions;
using Kadr.SharedKernel.Constants;
using Kadr.SharedKernel.Results;
using Kadr.SharedKernel.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Kadr.Infrastructure.Persistence.Leave;

public sealed class LeaveService(
    KadrDbContext db,
    ITenantContext tenantContext,
    ISubscriptionGuard subscriptionGuard,
    IAuditLogWriter audit,
    IWorkflowEngine workflowEngine,
    IFileStorage fileStorage) : ILeaveService
{
    public async Task<Result<IReadOnlyList<LeaveTypeDto>>> ListLeaveTypesAsync(
        CancellationToken cancellationToken = default)
    {
        var gate = await subscriptionGuard.EnsureFeatureAsync(FeatureKeys.Leave, cancellationToken)
            .ConfigureAwait(false);
        if (gate.IsFailure)
        {
            return Result.Failure<IReadOnlyList<LeaveTypeDto>>(gate.ErrorCode!, gate.ErrorMessage!);
        }

        var items = await db.LeaveTypes.AsNoTracking()
            .OrderBy(t => t.NameAr)
            .Select(t => new LeaveTypeDto(
                t.Id,
                t.NameAr,
                t.NameEn,
                t.Paid,
                t.DaysPerYear,
                t.RequiresApproval,
                t.RequiresAttachment,
                t.IsActive,
                t.SourceKey,
                t.IsCustomized))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return Result.Success<IReadOnlyList<LeaveTypeDto>>(items);
    }

    public async Task<Result<LeaveTypeDto>> UpdateLeaveTypeAsync(
        Guid id,
        UpdateLeaveTypeRequest request,
        CancellationToken cancellationToken = default)
    {
        var writable = await EnsureWritableLeaveAsync(cancellationToken).ConfigureAwait(false);
        if (writable.IsFailure)
        {
            return Result.Failure<LeaveTypeDto>(writable.ErrorCode!, writable.ErrorMessage!);
        }

        var entity = await db.LeaveTypes.SingleOrDefaultAsync(t => t.Id == id, cancellationToken)
            .ConfigureAwait(false);
        if (entity is null)
        {
            return Result.Failure<LeaveTypeDto>(ErrorCodes.LeaveNotFound, ErrorCodes.LeaveNotFound);
        }

        try
        {
            entity.Update(
                request.NameAr,
                request.NameEn,
                request.Paid,
                request.DaysPerYear,
                request.RequiresApproval,
                request.RequiresAttachment,
                entity.ExtrasJson);
            entity.SetActive(request.IsActive);
        }
        catch (ArgumentException)
        {
            return Result.Failure<LeaveTypeDto>(ErrorCodes.LeaveValidationFailed, ErrorCodes.LeaveValidationFailed);
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await audit.WriteAsync(
                tenantContext.TenantId!.Value,
                tenantContext.UserId,
                "leave.type.update",
                nameof(LeaveType),
                entity.Id,
                null,
                null,
                cancellationToken)
            .ConfigureAwait(false);

        return Result.Success(new LeaveTypeDto(
            entity.Id,
            entity.NameAr,
            entity.NameEn,
            entity.Paid,
            entity.DaysPerYear,
            entity.RequiresApproval,
            entity.RequiresAttachment,
            entity.IsActive,
            entity.SourceKey,
            entity.IsCustomized));
    }

    public async Task<Result<IReadOnlyList<LeaveBalanceDto>>> GetMyBalancesAsync(
        int? year,
        CancellationToken cancellationToken = default)
    {
        var gate = await subscriptionGuard.EnsureFeatureAsync(FeatureKeys.Leave, cancellationToken)
            .ConfigureAwait(false);
        if (gate.IsFailure)
        {
            return Result.Failure<IReadOnlyList<LeaveBalanceDto>>(gate.ErrorCode!, gate.ErrorMessage!);
        }

        var employee = await ResolveLinkedEmployeeAsync(cancellationToken).ConfigureAwait(false);
        if (employee is null)
        {
            return Result.Failure<IReadOnlyList<LeaveBalanceDto>>(
                ErrorCodes.AttendanceNoEmployee,
                ErrorCodes.AttendanceNoEmployee);
        }

        return await ListBalancesInternalAsync(year, employee.Id, null, cancellationToken).ConfigureAwait(false);
    }

    public Task<Result<IReadOnlyList<LeaveBalanceDto>>> ListBalancesAsync(
        int? year,
        Guid? employeeId,
        Guid? leaveTypeId,
        CancellationToken cancellationToken = default) =>
        ListBalancesInternalAsync(year, employeeId, leaveTypeId, cancellationToken);

    public async Task<Result<LeaveBalanceDto>> AdjustBalanceAsync(
        AdjustLeaveBalanceRequest request,
        CancellationToken cancellationToken = default)
    {
        var writable = await EnsureWritableLeaveAsync(cancellationToken).ConfigureAwait(false);
        if (writable.IsFailure)
        {
            return Result.Failure<LeaveBalanceDto>(writable.ErrorCode!, writable.ErrorMessage!);
        }

        if (string.IsNullOrWhiteSpace(request.Reason) || request.DeltaDays == 0)
        {
            return Result.Failure<LeaveBalanceDto>(ErrorCodes.LeaveValidationFailed, ErrorCodes.LeaveValidationFailed);
        }

        var balance = await EnsureBalanceAsync(
                request.EmployeeId,
                request.LeaveTypeId,
                request.Year,
                cancellationToken)
            .ConfigureAwait(false);
        if (balance.IsFailure)
        {
            return Result.Failure<LeaveBalanceDto>(balance.ErrorCode!, balance.ErrorMessage!);
        }

        var entity = balance.Value!;
        entity.AddAdjustment(request.DeltaDays);
        db.LeaveBalanceAdjustments.Add(LeaveBalanceAdjustment.Create(
            tenantContext.TenantId!.Value,
            request.EmployeeId,
            request.LeaveTypeId,
            request.Year,
            request.DeltaDays,
            request.Reason,
            tenantContext.UserId));
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await audit.WriteAsync(
                tenantContext.TenantId.Value,
                tenantContext.UserId,
                "leave.balance.adjusted",
                nameof(LeaveBalance),
                entity.Id,
                null,
                null,
                cancellationToken)
            .ConfigureAwait(false);

        var list = await ListBalancesInternalAsync(request.Year, request.EmployeeId, request.LeaveTypeId, cancellationToken)
            .ConfigureAwait(false);
        return list.IsFailure
            ? Result.Failure<LeaveBalanceDto>(list.ErrorCode!, list.ErrorMessage!)
            : Result.Success(list.Value![0]);
    }

    public async Task<Result<LeaveAttachmentDto>> UploadLeaveAttachmentAsync(
        Stream content,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        var writable = await EnsureWritableLeaveAsync(cancellationToken).ConfigureAwait(false);
        if (writable.IsFailure)
        {
            return Result.Failure<LeaveAttachmentDto>(writable.ErrorCode!, writable.ErrorMessage!);
        }

        if (tenantContext.TenantId is null)
        {
            return Result.Failure<LeaveAttachmentDto>(ErrorCodes.TenantNotFound, ErrorCodes.TenantNotFound);
        }

        var employee = await ResolveLinkedEmployeeAsync(cancellationToken).ConfigureAwait(false);
        if (employee is null)
        {
            return Result.Failure<LeaveAttachmentDto>(
                ErrorCodes.AttendanceNoEmployee,
                ErrorCodes.AttendanceNoEmployee);
        }

        var type = (contentType ?? string.Empty).Trim().ToLowerInvariant();
        if (type is not ("image/jpeg" or "image/png" or "image/jpg"))
        {
            return Result.Failure<LeaveAttachmentDto>(ErrorCodes.FileInvalidType, ErrorCodes.FileInvalidType);
        }

        if (string.IsNullOrWhiteSpace(fileName))
        {
            fileName = type.Contains("png", StringComparison.Ordinal) ? "leave.png" : "leave.jpg";
        }

        StoredFile stored;
        try
        {
            stored = await fileStorage
                .SaveAsync(
                    tenantContext.TenantId.Value,
                    FilePurpose.Document,
                    content,
                    fileName,
                    type == "image/jpg" ? "image/jpeg" : type,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (InvalidOperationException)
        {
            return Result.Failure<LeaveAttachmentDto>(ErrorCodes.FileInvalidType, ErrorCodes.FileInvalidType);
        }

        if (stored.SizeBytes is <= 0 or > 5_000_000)
        {
            await fileStorage.DeleteAsync(stored.StoragePath, cancellationToken).ConfigureAwait(false);
            return Result.Failure<LeaveAttachmentDto>(ErrorCodes.FileInvalidType, ErrorCodes.FileInvalidType);
        }

        var hash = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(stored.StoragePath + stored.SizeBytes)))
            .ToLowerInvariant();
        db.FileObjects.Add(FileObject.Create(
            stored.FileId,
            tenantContext.TenantId.Value,
            stored.StoragePath,
            stored.FileName,
            stored.ContentType,
            stored.SizeBytes,
            hash,
            FilePurpose.Document));
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(new LeaveAttachmentDto(stored.FileId));
    }

    public async Task<Result<LeaveRequestDto>> CreateLeaveRequestAsync(
        CreateLeaveRequestBody request,
        CancellationToken cancellationToken = default)
    {
        var writable = await EnsureWritableLeaveAsync(cancellationToken).ConfigureAwait(false);
        if (writable.IsFailure)
        {
            return Result.Failure<LeaveRequestDto>(writable.ErrorCode!, writable.ErrorMessage!);
        }

        var employee = await ResolveLinkedEmployeeAsync(cancellationToken).ConfigureAwait(false);
        if (employee is null)
        {
            return Result.Failure<LeaveRequestDto>(ErrorCodes.AttendanceNoEmployee, ErrorCodes.AttendanceNoEmployee);
        }

        var leaveType = await db.LeaveTypes.AsNoTracking()
            .SingleOrDefaultAsync(t => t.Id == request.LeaveTypeId && t.IsActive, cancellationToken)
            .ConfigureAwait(false);
        if (leaveType is null)
        {
            return Result.Failure<LeaveRequestDto>(ErrorCodes.LeaveNotFound, ErrorCodes.LeaveNotFound);
        }

        if (leaveType.RequiresAttachment && request.AttachmentFileId is null)
        {
            return Result.Failure<LeaveRequestDto>(
                ErrorCodes.LeaveAttachmentRequired,
                ErrorCodes.LeaveAttachmentRequired);
        }

        if (request.EndDate < request.StartDate)
        {
            return Result.Failure<LeaveRequestDto>(ErrorCodes.LeaveValidationFailed, ErrorCodes.LeaveValidationFailed);
        }

        var days = request.IsHalfDay ? 0.5m : CountInclusiveDays(request.StartDate, request.EndDate);
        var overlap = await db.LeaveRequests.AsNoTracking()
            .AnyAsync(
                r => r.EmployeeId == employee.Id
                     && r.Status != LeaveRequestStatus.Cancelled
                     && r.Status != LeaveRequestStatus.Rejected
                     && r.StartDate <= request.EndDate
                     && r.EndDate >= request.StartDate,
                cancellationToken)
            .ConfigureAwait(false);
        if (overlap)
        {
            return Result.Failure<LeaveRequestDto>(ErrorCodes.LeaveOverlap, ErrorCodes.LeaveOverlap);
        }

        var year = request.StartDate.Year;
        var bal = await EnsureBalanceAsync(employee.Id, leaveType.Id, year, cancellationToken).ConfigureAwait(false);
        if (bal.IsFailure)
        {
            return Result.Failure<LeaveRequestDto>(bal.ErrorCode!, bal.ErrorMessage!);
        }

        if (bal.Value!.Remaining < days)
        {
            return Result.Failure<LeaveRequestDto>(ErrorCodes.LeaveBalanceExceeded, ErrorCodes.LeaveBalanceExceeded);
        }

        LeaveRequest entity;
        try
        {
            entity = LeaveRequest.Create(
                tenantContext.TenantId!.Value,
                employee.Id,
                leaveType.Id,
                request.StartDate,
                request.EndDate,
                request.IsHalfDay,
                days,
                request.Reason,
                request.AttachmentFileId,
                tenantContext.UserId);
        }
        catch (ArgumentException)
        {
            return Result.Failure<LeaveRequestDto>(ErrorCodes.LeaveValidationFailed, ErrorCodes.LeaveValidationFailed);
        }

        db.LeaveRequests.Add(entity);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var wf = await workflowEngine.StartAsync(
                WorkflowRequestType.Leave,
                entity.Id,
                employee.Id,
                cancellationToken)
            .ConfigureAwait(false);
        if (wf.IsFailure)
        {
            return Result.Failure<LeaveRequestDto>(wf.ErrorCode!, wf.ErrorMessage!);
        }

        entity.AttachWorkflow(wf.Value);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await audit.WriteAsync(
                tenantContext.TenantId.Value,
                tenantContext.UserId,
                "leave.request.create",
                nameof(LeaveRequest),
                entity.Id,
                null,
                null,
                cancellationToken)
            .ConfigureAwait(false);

        return Result.Success(await MapLeaveAsync(entity, cancellationToken).ConfigureAwait(false));
    }

    public async Task<Result<PagedLeaveRequestsDto>> ListMyLeaveRequestsAsync(
        string? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var gate = await subscriptionGuard.EnsureFeatureAsync(FeatureKeys.Leave, cancellationToken)
            .ConfigureAwait(false);
        if (gate.IsFailure)
        {
            return Result.Failure<PagedLeaveRequestsDto>(gate.ErrorCode!, gate.ErrorMessage!);
        }

        var employee = await ResolveLinkedEmployeeAsync(cancellationToken).ConfigureAwait(false);
        if (employee is null)
        {
            return Result.Failure<PagedLeaveRequestsDto>(
                ErrorCodes.AttendanceNoEmployee,
                ErrorCodes.AttendanceNoEmployee);
        }

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = db.LeaveRequests.AsNoTracking().Where(r => r.EmployeeId == employee.Id);
        if (!string.IsNullOrWhiteSpace(status)
            && Enum.TryParse<LeaveRequestStatus>(status, true, out var st))
        {
            query = query.Where(r => r.Status == st);
        }

        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var ids = await query.OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => r.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var items = new List<LeaveRequestDto>();
        foreach (var id in ids)
        {
            var row = await db.LeaveRequests.AsNoTracking().SingleAsync(r => r.Id == id, cancellationToken)
                .ConfigureAwait(false);
            items.Add(await MapLeaveAsync(row, cancellationToken).ConfigureAwait(false));
        }

        return Result.Success(new PagedLeaveRequestsDto(items, page, pageSize, total));
    }

    public async Task<Result<LeaveRequestDto>> GetLeaveRequestAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var gate = await subscriptionGuard.EnsureFeatureAsync(FeatureKeys.Leave, cancellationToken)
            .ConfigureAwait(false);
        if (gate.IsFailure)
        {
            return Result.Failure<LeaveRequestDto>(gate.ErrorCode!, gate.ErrorMessage!);
        }

        var entity = await db.LeaveRequests.AsNoTracking()
            .SingleOrDefaultAsync(r => r.Id == id, cancellationToken)
            .ConfigureAwait(false);
        return entity is null
            ? Result.Failure<LeaveRequestDto>(ErrorCodes.LeaveNotFound, ErrorCodes.LeaveNotFound)
            : Result.Success(await MapLeaveAsync(entity, cancellationToken).ConfigureAwait(false));
    }

    public async Task<Result<LeaveRequestDto>> CancelLeaveRequestAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var writable = await EnsureWritableLeaveAsync(cancellationToken).ConfigureAwait(false);
        if (writable.IsFailure)
        {
            return Result.Failure<LeaveRequestDto>(writable.ErrorCode!, writable.ErrorMessage!);
        }

        var employee = await ResolveLinkedEmployeeAsync(cancellationToken).ConfigureAwait(false);
        var entity = await db.LeaveRequests.SingleOrDefaultAsync(r => r.Id == id, cancellationToken)
            .ConfigureAwait(false);
        if (entity is null || employee is null || entity.EmployeeId != employee.Id)
        {
            return Result.Failure<LeaveRequestDto>(ErrorCodes.LeaveNotFound, ErrorCodes.LeaveNotFound);
        }

        try
        {
            entity.Cancel();
        }
        catch (InvalidOperationException)
        {
            return Result.Failure<LeaveRequestDto>(ErrorCodes.LeaveValidationFailed, ErrorCodes.LeaveValidationFailed);
        }

        await workflowEngine.CancelForSubjectAsync(WorkflowRequestType.Leave, entity.Id, cancellationToken)
            .ConfigureAwait(false);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(await MapLeaveAsync(entity, cancellationToken).ConfigureAwait(false));
    }

    public async Task<Result<PermissionRequestDto>> CreatePermissionAsync(
        CreatePermissionRequestBody request,
        CancellationToken cancellationToken = default)
    {
        var writable = await EnsureWritableLeaveAsync(cancellationToken).ConfigureAwait(false);
        if (writable.IsFailure)
        {
            return Result.Failure<PermissionRequestDto>(writable.ErrorCode!, writable.ErrorMessage!);
        }

        var employee = await ResolveLinkedEmployeeAsync(cancellationToken).ConfigureAwait(false);
        if (employee is null)
        {
            return Result.Failure<PermissionRequestDto>(
                ErrorCodes.AttendanceNoEmployee,
                ErrorCodes.AttendanceNoEmployee);
        }

        var hours = (decimal)(request.EndTime - request.StartTime).TotalHours;
        HourlyPermissionRequest entity;
        try
        {
            entity = HourlyPermissionRequest.Create(
                tenantContext.TenantId!.Value,
                employee.Id,
                request.WorkDate,
                request.StartTime,
                request.EndTime,
                Math.Round(hours, 2),
                request.Reason,
                tenantContext.UserId);
        }
        catch (ArgumentException)
        {
            return Result.Failure<PermissionRequestDto>(
                ErrorCodes.LeaveValidationFailed,
                ErrorCodes.LeaveValidationFailed);
        }

        db.HourlyPermissionRequests.Add(entity);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        var wf = await workflowEngine.StartAsync(
                WorkflowRequestType.Permission,
                entity.Id,
                employee.Id,
                cancellationToken)
            .ConfigureAwait(false);
        if (wf.IsFailure)
        {
            return Result.Failure<PermissionRequestDto>(wf.ErrorCode!, wf.ErrorMessage!);
        }

        entity.AttachWorkflow(wf.Value);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(await MapPermissionAsync(entity, cancellationToken).ConfigureAwait(false));
    }

    public async Task<Result<IReadOnlyList<PermissionRequestDto>>> ListMyPermissionsAsync(
        CancellationToken cancellationToken = default)
    {
        var gate = await subscriptionGuard.EnsureFeatureAsync(FeatureKeys.Leave, cancellationToken)
            .ConfigureAwait(false);
        if (gate.IsFailure)
        {
            return Result.Failure<IReadOnlyList<PermissionRequestDto>>(gate.ErrorCode!, gate.ErrorMessage!);
        }

        var employee = await ResolveLinkedEmployeeAsync(cancellationToken).ConfigureAwait(false);
        if (employee is null)
        {
            return Result.Failure<IReadOnlyList<PermissionRequestDto>>(
                ErrorCodes.AttendanceNoEmployee,
                ErrorCodes.AttendanceNoEmployee);
        }

        var rows = await db.HourlyPermissionRequests.AsNoTracking()
            .Where(p => p.EmployeeId == employee.Id)
            .OrderByDescending(p => p.CreatedAt)
            .Take(100)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var list = new List<PermissionRequestDto>();
        foreach (var row in rows)
        {
            list.Add(await MapPermissionAsync(row, cancellationToken).ConfigureAwait(false));
        }

        return Result.Success<IReadOnlyList<PermissionRequestDto>>(list);
    }

    public async Task<Result<IReadOnlyList<WorkflowDefinitionDto>>> ListWorkflowsAsync(
        CancellationToken cancellationToken = default)
    {
        var gate = await subscriptionGuard.EnsureFeatureAsync(FeatureKeys.Workflows, cancellationToken)
            .ConfigureAwait(false);
        if (gate.IsFailure)
        {
            return Result.Failure<IReadOnlyList<WorkflowDefinitionDto>>(gate.ErrorCode!, gate.ErrorMessage!);
        }

        var workflows = await db.ApprovalWorkflows.AsNoTracking()
            .OrderBy(w => w.RequestType)
            .ThenBy(w => w.NameAr)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var result = new List<WorkflowDefinitionDto>();
        foreach (var w in workflows)
        {
            result.Add(await MapWorkflowAsync(w, cancellationToken).ConfigureAwait(false));
        }

        return Result.Success<IReadOnlyList<WorkflowDefinitionDto>>(result);
    }

    public async Task<Result<WorkflowDefinitionDto>> UpsertWorkflowAsync(
        Guid? id,
        UpsertWorkflowRequest request,
        CancellationToken cancellationToken = default)
    {
        var writable = await EnsureWritableFeatureAsync(FeatureKeys.Workflows, cancellationToken)
            .ConfigureAwait(false);
        if (writable.IsFailure)
        {
            return Result.Failure<WorkflowDefinitionDto>(writable.ErrorCode!, writable.ErrorMessage!);
        }

        if (!Enum.TryParse<WorkflowRequestType>(request.RequestType, true, out var requestType)
            || request.Steps.Count == 0)
        {
            return Result.Failure<WorkflowDefinitionDto>(
                ErrorCodes.LeaveValidationFailed,
                ErrorCodes.LeaveValidationFailed);
        }

        ApprovalWorkflow workflow;
        if (id is Guid existingId)
        {
            workflow = (await db.ApprovalWorkflows.SingleOrDefaultAsync(w => w.Id == existingId, cancellationToken)
                .ConfigureAwait(false))!;
            if (workflow is null)
            {
                return Result.Failure<WorkflowDefinitionDto>(ErrorCodes.WorkflowsNotFound, ErrorCodes.WorkflowsNotFound);
            }

            workflow.Update(request.NameAr, request.NameEn, request.IsActive, request.IsDefault);
        }
        else
        {
            workflow = ApprovalWorkflow.Create(
                tenantContext.TenantId!.Value,
                request.NameAr,
                request.NameEn,
                requestType,
                request.IsDefault);
            db.ApprovalWorkflows.Add(workflow);
        }

        if (request.IsDefault)
        {
            var others = await db.ApprovalWorkflows
                .Where(w => w.RequestType == requestType && w.Id != workflow.Id && w.IsDefault)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            foreach (var o in others)
            {
                o.ClearDefault();
            }
        }

        var oldSteps = await db.ApprovalWorkflowSteps
            .Where(s => s.WorkflowId == workflow.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        db.ApprovalWorkflowSteps.RemoveRange(oldSteps);

        try
        {
            foreach (var step in request.Steps.OrderBy(s => s.StepOrder))
            {
                if (!Enum.TryParse<ApproverKind>(step.ApproverKind, true, out var kind))
                {
                    return Result.Failure<WorkflowDefinitionDto>(
                        ErrorCodes.LeaveValidationFailed,
                        ErrorCodes.LeaveValidationFailed);
                }

                db.ApprovalWorkflowSteps.Add(ApprovalWorkflowStep.Create(
                    tenantContext.TenantId!.Value,
                    workflow.Id,
                    step.StepOrder,
                    kind,
                    step.RoleId,
                    step.UserId,
                    step.IsFinal));
            }
        }
        catch (ArgumentException)
        {
            return Result.Failure<WorkflowDefinitionDto>(
                ErrorCodes.LeaveValidationFailed,
                ErrorCodes.LeaveValidationFailed);
        }

        workflow.Touch();
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(await MapWorkflowAsync(workflow, cancellationToken).ConfigureAwait(false));
    }

    public async Task<Result<PagedRequestInboxDto>> ListInboxAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var gate = await subscriptionGuard.EnsureFeatureAsync(FeatureKeys.Leave, cancellationToken)
            .ConfigureAwait(false);
        if (gate.IsFailure)
        {
            return Result.Failure<PagedRequestInboxDto>(gate.ErrorCode!, gate.ErrorMessage!);
        }

        if (tenantContext.UserId is null)
        {
            return Result.Failure<PagedRequestInboxDto>(ErrorCodes.TenantNotFound, ErrorCodes.TenantNotFound);
        }

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var userId = tenantContext.UserId.Value;
        var myEmployeeId = await db.Employees.AsNoTracking()
            .Where(e => e.UserId == userId)
            .Select(e => (Guid?)e.Id)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        var roleIds = await db.UserRoles.AsNoTracking()
            .Where(ur => ur.UserId == userId)
            .Select(ur => ur.RoleId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var query = db.WorkflowTasks.AsNoTracking()
            .Where(t => t.IsOpen
                        && (t.AssignedUserId == userId
                            || (myEmployeeId != null && t.ManagerEmployeeId == myEmployeeId)
                            || (t.RoleId != null && roleIds.Contains(t.RoleId.Value))));

        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var tasks = await query.OrderBy(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var items = new List<RequestInboxItemDto>();
        foreach (var task in tasks)
        {
            var instance = await db.WorkflowInstances.AsNoTracking()
                .SingleAsync(i => i.Id == task.InstanceId, cancellationToken)
                .ConfigureAwait(false);
            var requester = await db.Employees.AsNoTracking()
                .SingleAsync(e => e.Id == instance.RequesterEmployeeId, cancellationToken)
                .ConfigureAwait(false);
            items.Add(new RequestInboxItemDto(
                task.Id,
                instance.Id,
                instance.RequestType.ToString(),
                instance.SubjectId,
                requester.Id,
                requester.FullNameAr,
                requester.FullNameEn ?? requester.FullNameAr,
                task.StepOrder,
                task.CreatedAt,
                $"{instance.RequestType}:{instance.SubjectId:N}"));
        }

        return Result.Success(new PagedRequestInboxDto(items, page, pageSize, total));
    }

    public async Task<Result> ApproveTaskAsync(Guid taskId, CancellationToken cancellationToken = default)
    {
        return await DecideAsync(taskId, approved: true, null, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result> RejectTaskAsync(
        Guid taskId,
        RejectRequestBody request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return Result.Failure(ErrorCodes.RequestsRejectReasonRequired, ErrorCodes.RequestsRejectReasonRequired);
        }

        return await DecideAsync(taskId, approved: false, request.Reason.Trim(), cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<Result> DecideAsync(
        Guid taskId,
        bool approved,
        string? rejectReason,
        CancellationToken cancellationToken)
    {
        var leaveGate = await EnsureWritableFeatureAsync(FeatureKeys.Leave, cancellationToken)
            .ConfigureAwait(false);
        if (leaveGate.IsFailure)
        {
            var att = await EnsureWritableFeatureAsync(FeatureKeys.Attendance, cancellationToken)
                .ConfigureAwait(false);
            if (att.IsFailure)
            {
                return Result.Failure(leaveGate.ErrorCode!, leaveGate.ErrorMessage!);
            }
        }

        if (tenantContext.UserId is null || tenantContext.TenantId is null)
        {
            return Result.Failure(ErrorCodes.TenantNotFound, ErrorCodes.TenantNotFound);
        }

        var task = await db.WorkflowTasks.SingleOrDefaultAsync(t => t.Id == taskId, cancellationToken)
            .ConfigureAwait(false);
        if (task is null || !task.IsOpen)
        {
            return Result.Failure(ErrorCodes.RequestsNotFound, ErrorCodes.RequestsNotFound);
        }

        if (!await CanActOnTaskAsync(task, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure(ErrorCodes.RequestsForbidden, ErrorCodes.RequestsForbidden);
        }

        try
        {
            task.Decide(tenantContext.UserId.Value, approved, rejectReason);
        }
        catch (ArgumentException)
        {
            return Result.Failure(ErrorCodes.RequestsRejectReasonRequired, ErrorCodes.RequestsRejectReasonRequired);
        }

        var instance = await db.WorkflowInstances.SingleAsync(i => i.Id == task.InstanceId, cancellationToken)
            .ConfigureAwait(false);

        if (!approved)
        {
            instance.CompleteRejected();
            await ApplySubjectDecisionAsync(instance, approved: false, rejectReason!, cancellationToken)
                .ConfigureAwait(false);
            await RaiseRequesterDecisionAsync(instance, approved: false, rejectReason, cancellationToken)
                .ConfigureAwait(false);
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return Result.Success();
        }

        var next = await db.ApprovalWorkflowSteps
            .Where(s => s.WorkflowId == instance.WorkflowId && s.StepOrder > task.StepOrder)
            .OrderBy(s => s.StepOrder)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (next is null)
        {
            instance.CompleteApproved();
            await ApplySubjectDecisionAsync(instance, approved: true, null, cancellationToken)
                .ConfigureAwait(false);
            await RaiseRequesterDecisionAsync(instance, approved: true, null, cancellationToken)
                .ConfigureAwait(false);
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return Result.Success();
        }

        instance.AdvanceTo(next.StepOrder);
        var requester = await db.Employees.AsNoTracking()
            .SingleAsync(e => e.Id == instance.RequesterEmployeeId, cancellationToken)
            .ConfigureAwait(false);
        Guid? managerEmployeeId = null;
        Guid? assignedUserId = next.UserId;
        if (next.ApproverKind == ApproverKind.DirectManager)
        {
            managerEmployeeId = requester.ManagerEmployeeId;
            if (managerEmployeeId is Guid mid)
            {
                assignedUserId = await db.Employees.AsNoTracking()
                    .Where(e => e.Id == mid)
                    .Select(e => e.UserId)
                    .FirstOrDefaultAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        var nextTask = WorkflowTask.Open(
            instance.TenantId,
            instance.Id,
            next.StepOrder,
            next.ApproverKind,
            next.RoleId,
            assignedUserId,
            managerEmployeeId);
        db.WorkflowTasks.Add(nextTask);
        if (assignedUserId is Guid nextApproverId)
        {
            instance.NotifyApprovalRequired(
                nextApproverId,
                requester.FullNameAr,
                requester.FullNameEn ?? requester.FullNameAr);
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    private async Task ApplySubjectDecisionAsync(
        WorkflowInstance instance,
        bool approved,
        string? rejectReason,
        CancellationToken cancellationToken)
    {
        var userId = tenantContext.UserId!.Value;
        switch (instance.RequestType)
        {
            case WorkflowRequestType.Leave:
            {
                var leave = await db.LeaveRequests.SingleAsync(r => r.Id == instance.SubjectId, cancellationToken)
                    .ConfigureAwait(false);
                if (approved)
                {
                    leave.Approve(userId);
                    var bal = await EnsureBalanceAsync(
                            leave.EmployeeId,
                            leave.LeaveTypeId,
                            leave.StartDate.Year,
                            cancellationToken)
                        .ConfigureAwait(false);
                    if (bal.IsSuccess)
                    {
                        bal.Value!.AddUsed(leave.Days);
                    }

                    await MarkAttendanceLeaveAsync(leave, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    leave.Reject(userId, rejectReason!);
                }

                break;
            }
            case WorkflowRequestType.Permission:
            {
                var perm = await db.HourlyPermissionRequests
                    .SingleAsync(r => r.Id == instance.SubjectId, cancellationToken)
                    .ConfigureAwait(false);
                if (approved)
                {
                    perm.Approve(userId);
                }
                else
                {
                    perm.Reject(userId, rejectReason!);
                }

                break;
            }
            case WorkflowRequestType.AttendanceCorrection:
            {
                var corr = await db.AttendanceCorrectionRequests
                    .SingleAsync(c => c.Id == instance.SubjectId, cancellationToken)
                    .ConfigureAwait(false);
                if (approved)
                {
                    await ApplyCorrectionAsync(corr, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    corr.Reject(userId, rejectReason!);
                }

                break;
            }
        }

        await audit.WriteAsync(
                tenantContext.TenantId!.Value,
                userId,
                approved ? "requests.approved" : "requests.rejected",
                instance.RequestType.ToString(),
                instance.SubjectId,
                null,
                null,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task ApplyCorrectionAsync(
        AttendanceCorrectionRequest entity,
        CancellationToken cancellationToken)
    {
        var tenant = await db.Tenants.AsNoTracking()
            .SingleAsync(t => t.Id == tenantContext.TenantId!.Value, cancellationToken)
            .ConfigureAwait(false);
        entity.Approve(tenantContext.UserId!.Value);

        var summary = await db.AttendanceDaySummaries
            .SingleOrDefaultAsync(
                s => s.EmployeeId == entity.EmployeeId && s.WorkDate == entity.WorkDate,
                cancellationToken)
            .ConfigureAwait(false);
        if (summary is null)
        {
            summary = AttendanceDaySummary.Create(
                tenant.Id,
                entity.EmployeeId,
                entity.WorkDate,
                null,
                AttendanceDayStatus.Present);
            db.AttendanceDaySummaries.Add(summary);
        }

        var offset = TenantTimeZones.ToUtc(entity.WorkDate, entity.ProposedTime, tenant.TimeZone);
        if (entity.ProblemType is "ForgotIn" or "WrongTime")
        {
            summary.ApplyManualCorrection(
                AttendanceDayStatus.Present,
                offset,
                summary.LastOutAt,
                summary.WorkedMinutes,
                0);
        }
        else if (entity.ProblemType == "ForgotOut")
        {
            var first = summary.FirstInAt ?? offset.AddHours(-8);
            var worked = (int)Math.Max(0, (offset - first).TotalMinutes);
            summary.ApplyManualCorrection(AttendanceDayStatus.Present, first, offset, worked, summary.LateMinutes);
        }
        else
        {
            summary.ApplyManualCorrection(
                AttendanceDayStatus.Present,
                summary.FirstInAt ?? offset,
                summary.LastOutAt ?? offset,
                summary.WorkedMinutes,
                summary.LateMinutes);
        }
    }

    private async Task MarkAttendanceLeaveAsync(LeaveRequest leave, CancellationToken cancellationToken)
    {
        for (var d = leave.StartDate; d <= leave.EndDate; d = d.AddDays(1))
        {
            var summary = await db.AttendanceDaySummaries
                .SingleOrDefaultAsync(s => s.EmployeeId == leave.EmployeeId && s.WorkDate == d, cancellationToken)
                .ConfigureAwait(false);
            if (summary is null)
            {
                summary = AttendanceDaySummary.Create(
                    leave.TenantId,
                    leave.EmployeeId,
                    d,
                    null,
                    AttendanceDayStatus.Leave);
                db.AttendanceDaySummaries.Add(summary);
            }
            else
            {
                summary.ApplyManualCorrection(
                    AttendanceDayStatus.Leave,
                    summary.FirstInAt,
                    summary.LastOutAt,
                    summary.WorkedMinutes,
                    0);
            }
        }
    }

    private async Task RaiseRequesterDecisionAsync(
        WorkflowInstance instance,
        bool approved,
        string? rejectReason,
        CancellationToken cancellationToken)
    {
        var requesterUserId = await db.Employees.AsNoTracking()
            .Where(e => e.Id == instance.RequesterEmployeeId)
            .Select(e => e.UserId)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (requesterUserId is Guid uid)
        {
            instance.NotifyDecision(uid, approved, rejectReason);
        }
    }

    private async Task<bool> CanActOnTaskAsync(WorkflowTask task, CancellationToken cancellationToken)
    {
        var userId = tenantContext.UserId!.Value;
        if (task.AssignedUserId == userId)
        {
            return true;
        }

        var myEmployeeId = await db.Employees.AsNoTracking()
            .Where(e => e.UserId == userId)
            .Select(e => (Guid?)e.Id)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (myEmployeeId != null && task.ManagerEmployeeId == myEmployeeId)
        {
            return true;
        }

        if (task.RoleId is Guid roleId)
        {
            return await db.UserRoles.AsNoTracking()
                .AnyAsync(ur => ur.UserId == userId && ur.RoleId == roleId, cancellationToken)
                .ConfigureAwait(false);
        }

        return false;
    }

    private async Task<Result<IReadOnlyList<LeaveBalanceDto>>> ListBalancesInternalAsync(
        int? year,
        Guid? employeeId,
        Guid? leaveTypeId,
        CancellationToken cancellationToken)
    {
        var gate = await subscriptionGuard.EnsureFeatureAsync(FeatureKeys.Leave, cancellationToken)
            .ConfigureAwait(false);
        if (gate.IsFailure)
        {
            return Result.Failure<IReadOnlyList<LeaveBalanceDto>>(gate.ErrorCode!, gate.ErrorMessage!);
        }

        var y = year ?? DateTime.UtcNow.Year;
        if (employeeId is Guid eid)
        {
            var types = await db.LeaveTypes.AsNoTracking()
                .Where(t => t.IsActive && (leaveTypeId == null || t.Id == leaveTypeId))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            foreach (var t in types)
            {
                await EnsureBalanceAsync(eid, t.Id, y, cancellationToken).ConfigureAwait(false);
            }

            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        var query = db.LeaveBalances.AsNoTracking().Where(b => b.Year == y);
        if (employeeId is Guid e2)
        {
            query = query.Where(b => b.EmployeeId == e2);
        }

        if (leaveTypeId is Guid lt)
        {
            query = query.Where(b => b.LeaveTypeId == lt);
        }

        var balances = await query.Take(100).ToListAsync(cancellationToken).ConfigureAwait(false);
        var result = new List<LeaveBalanceDto>();
        foreach (var b in balances)
        {
            var emp = await db.Employees.AsNoTracking().SingleAsync(e => e.Id == b.EmployeeId, cancellationToken)
                .ConfigureAwait(false);
            var type = await db.LeaveTypes.AsNoTracking().SingleAsync(t => t.Id == b.LeaveTypeId, cancellationToken)
                .ConfigureAwait(false);
            result.Add(new LeaveBalanceDto(
                emp.Id,
                emp.FullNameAr,
                emp.FullNameEn ?? emp.FullNameAr,
                type.Id,
                type.NameAr,
                type.NameEn,
                b.Year,
                b.Entitled,
                b.Accrued,
                b.Used,
                b.Adjustment,
                b.CarriedIn,
                b.Remaining));
        }

        return Result.Success<IReadOnlyList<LeaveBalanceDto>>(result);
    }

    private async Task<Result<LeaveBalance>> EnsureBalanceAsync(
        Guid employeeId,
        Guid leaveTypeId,
        int year,
        CancellationToken cancellationToken)
    {
        var existing = await db.LeaveBalances
            .SingleOrDefaultAsync(
                b => b.EmployeeId == employeeId && b.LeaveTypeId == leaveTypeId && b.Year == year,
                cancellationToken)
            .ConfigureAwait(false);
        if (existing is not null)
        {
            return Result.Success(existing);
        }

        var type = await db.LeaveTypes.AsNoTracking()
            .SingleOrDefaultAsync(t => t.Id == leaveTypeId, cancellationToken)
            .ConfigureAwait(false);
        if (type is null)
        {
            return Result.Failure<LeaveBalance>(ErrorCodes.LeaveNotFound, ErrorCodes.LeaveNotFound);
        }

        // Entitlement comes from LeaveType.DaysPerYear seed data ([VERIFY] in templates) — never hard-coded here.
        var created = LeaveBalance.Create(
            tenantContext.TenantId!.Value,
            employeeId,
            leaveTypeId,
            year,
            type.DaysPerYear ?? 0);
        db.LeaveBalances.Add(created);
        return Result.Success(created);
    }

    private async Task<Employee?> ResolveLinkedEmployeeAsync(CancellationToken cancellationToken)
    {
        if (tenantContext.UserId is null)
        {
            return null;
        }

        return await db.Employees
            .SingleOrDefaultAsync(e => e.UserId == tenantContext.UserId, cancellationToken)
            .ConfigureAwait(false);
    }

    private Task<Result> EnsureWritableLeaveAsync(CancellationToken cancellationToken) =>
        EnsureWritableFeatureAsync(FeatureKeys.Leave, cancellationToken);

    private async Task<Result> EnsureWritableFeatureAsync(string featureKey, CancellationToken cancellationToken)
    {
        var writable = await subscriptionGuard.EnsureWritableAsync(cancellationToken).ConfigureAwait(false);
        if (writable.IsFailure)
        {
            return writable;
        }

        return await subscriptionGuard.EnsureFeatureAsync(featureKey, cancellationToken).ConfigureAwait(false);
    }

    private async Task<LeaveRequestDto> MapLeaveAsync(LeaveRequest r, CancellationToken cancellationToken)
    {
        var emp = await db.Employees.AsNoTracking().SingleAsync(e => e.Id == r.EmployeeId, cancellationToken)
            .ConfigureAwait(false);
        var type = await db.LeaveTypes.AsNoTracking().SingleAsync(t => t.Id == r.LeaveTypeId, cancellationToken)
            .ConfigureAwait(false);
        return new LeaveRequestDto(
            r.Id,
            emp.Id,
            emp.FullNameAr,
            emp.FullNameEn ?? emp.FullNameAr,
            type.Id,
            type.NameAr,
            type.NameEn,
            r.StartDate,
            r.EndDate,
            r.IsHalfDay,
            r.Days,
            r.Reason,
            r.AttachmentFileId,
            r.Status.ToString(),
            r.WorkflowInstanceId,
            r.CreatedAt,
            r.RejectReason);
    }

    private async Task<PermissionRequestDto> MapPermissionAsync(
        HourlyPermissionRequest r,
        CancellationToken cancellationToken)
    {
        var emp = await db.Employees.AsNoTracking().SingleAsync(e => e.Id == r.EmployeeId, cancellationToken)
            .ConfigureAwait(false);
        return new PermissionRequestDto(
            r.Id,
            emp.Id,
            emp.FullNameAr,
            emp.FullNameEn ?? emp.FullNameAr,
            r.WorkDate,
            r.StartTime,
            r.EndTime,
            r.Hours,
            r.Reason,
            r.Status.ToString(),
            r.WorkflowInstanceId,
            r.CreatedAt,
            r.RejectReason);
    }

    private async Task<WorkflowDefinitionDto> MapWorkflowAsync(
        ApprovalWorkflow w,
        CancellationToken cancellationToken)
    {
        var steps = await db.ApprovalWorkflowSteps.AsNoTracking()
            .Where(s => s.WorkflowId == w.Id)
            .OrderBy(s => s.StepOrder)
            .Select(s => new WorkflowStepDto(
                s.Id,
                s.StepOrder,
                s.ApproverKind.ToString(),
                s.RoleId,
                s.UserId,
                s.IsFinal))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return new WorkflowDefinitionDto(
            w.Id,
            w.NameAr,
            w.NameEn,
            w.RequestType.ToString(),
            w.IsActive,
            w.IsDefault,
            steps);
    }

    private static decimal CountInclusiveDays(DateOnly start, DateOnly end) =>
        end.DayNumber - start.DayNumber + 1;
}
