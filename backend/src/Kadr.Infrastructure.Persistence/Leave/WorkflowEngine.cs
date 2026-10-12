using Kadr.Platform.Application.Abstractions;
using Kadr.Platform.Domain.Employees;
using Kadr.Platform.Domain.Workflows;
using Kadr.SharedKernel.Constants;
using Kadr.SharedKernel.Results;
using Kadr.SharedKernel.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Kadr.Infrastructure.Persistence.Leave;

public sealed class WorkflowEngine(
    KadrDbContext db,
    ITenantContext tenantContext) : IWorkflowEngine
{
    public async Task<Result<Guid>> StartAsync(
        WorkflowRequestType requestType,
        Guid subjectId,
        Guid requesterEmployeeId,
        CancellationToken cancellationToken = default)
    {
        if (tenantContext.TenantId is null)
        {
            return Result.Failure<Guid>(ErrorCodes.TenantNotFound, ErrorCodes.TenantNotFound);
        }

        var tenantId = tenantContext.TenantId.Value;
        var workflow = await db.ApprovalWorkflows
            .Where(w => w.RequestType == requestType && w.IsActive)
            .OrderByDescending(w => w.IsDefault)
            .ThenBy(w => w.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (workflow is null)
        {
            workflow = ApprovalWorkflow.Create(
                tenantId,
                DefaultNameAr(requestType),
                DefaultNameEn(requestType),
                requestType,
                isDefault: true);
            db.ApprovalWorkflows.Add(workflow);
            db.ApprovalWorkflowSteps.Add(ApprovalWorkflowStep.Create(
                tenantId,
                workflow.Id,
                1,
                ApproverKind.DirectManager,
                null,
                null,
                isFinal: true));
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        var steps = await db.ApprovalWorkflowSteps
            .Where(s => s.WorkflowId == workflow.Id)
            .OrderBy(s => s.StepOrder)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        if (steps.Count == 0)
        {
            return Result.Failure<Guid>(ErrorCodes.WorkflowsNotFound, ErrorCodes.WorkflowsNotFound);
        }

        var first = steps[0];
        var instance = WorkflowInstance.Start(
            tenantId,
            workflow.Id,
            requestType,
            subjectId,
            requesterEmployeeId,
            first.StepOrder);
        db.WorkflowInstances.Add(instance);

        var requester = await db.Employees.AsNoTracking()
            .SingleOrDefaultAsync(e => e.Id == requesterEmployeeId, cancellationToken)
            .ConfigureAwait(false);
        var task = await OpenTaskAsync(instance, first, requester, cancellationToken).ConfigureAwait(false);
        db.WorkflowTasks.Add(task);
        if (task.AssignedUserId is Guid approverId)
        {
            var nameAr = requester?.FullNameAr ?? "موظف";
            var nameEn = requester?.FullNameEn ?? requester?.FullNameAr ?? "an employee";
            instance.NotifyApprovalRequired(approverId, nameAr, nameEn);
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(instance.Id);
    }

    public async Task<Result> CancelForSubjectAsync(
        WorkflowRequestType requestType,
        Guid subjectId,
        CancellationToken cancellationToken = default)
    {
        var instance = await db.WorkflowInstances
            .Where(i => i.RequestType == requestType && i.SubjectId == subjectId && i.Status == WorkflowInstanceStatus.Pending)
            .OrderByDescending(i => i.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (instance is null)
        {
            return Result.Success();
        }

        instance.Cancel();
        var open = await db.WorkflowTasks
            .Where(t => t.InstanceId == instance.Id && t.IsOpen)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        foreach (var task in open)
        {
            task.CloseCancelled();
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    public async Task<WorkflowTask> OpenTaskAsync(
        WorkflowInstance instance,
        ApprovalWorkflowStep step,
        Employee? requester,
        CancellationToken cancellationToken)
    {
        Guid? managerEmployeeId = null;
        Guid? assignedUserId = step.UserId;
        if (step.ApproverKind == ApproverKind.DirectManager)
        {
            managerEmployeeId = requester?.ManagerEmployeeId;
            if (managerEmployeeId is Guid mid)
            {
                assignedUserId = await db.Employees.AsNoTracking()
                    .Where(e => e.Id == mid)
                    .Select(e => e.UserId)
                    .FirstOrDefaultAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        return WorkflowTask.Open(
            instance.TenantId,
            instance.Id,
            step.StepOrder,
            step.ApproverKind,
            step.RoleId,
            assignedUserId,
            managerEmployeeId);
    }

    private static string DefaultNameAr(WorkflowRequestType type) => type switch
    {
        WorkflowRequestType.Leave => "اعتماد الإجازة",
        WorkflowRequestType.Permission => "اعتماد الإذن",
        _ => "اعتماد تصحيح الحضور",
    };

    private static string DefaultNameEn(WorkflowRequestType type) => type switch
    {
        WorkflowRequestType.Leave => "Leave approval",
        WorkflowRequestType.Permission => "Permission approval",
        _ => "Attendance correction approval",
    };
}
