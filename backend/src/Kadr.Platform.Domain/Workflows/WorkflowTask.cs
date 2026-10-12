using Kadr.SharedKernel.Domain;
using Kadr.SharedKernel.Ids;

namespace Kadr.Platform.Domain.Workflows;

public sealed class WorkflowTask : Entity, ITenantOwned
{
    private WorkflowTask()
    {
    }

    public Guid TenantId { get; private set; }
    public Guid InstanceId { get; private set; }
    public int StepOrder { get; private set; }
    public ApproverKind ApproverKind { get; private set; }
    public Guid? RoleId { get; private set; }
    public Guid? AssignedUserId { get; private set; }
    public Guid? ManagerEmployeeId { get; private set; }
    public bool IsOpen { get; private set; } = true;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? DecidedAt { get; private set; }
    public Guid? DecidedByUserId { get; private set; }
    public bool? Approved { get; private set; }
    public string? RejectReason { get; private set; }

    public static WorkflowTask Open(
        Guid tenantId,
        Guid instanceId,
        int stepOrder,
        ApproverKind kind,
        Guid? roleId,
        Guid? assignedUserId,
        Guid? managerEmployeeId)
    {
        return new WorkflowTask
        {
            Id = GuidV7.NewGuid(),
            TenantId = tenantId,
            InstanceId = instanceId,
            StepOrder = stepOrder,
            ApproverKind = kind,
            RoleId = roleId,
            AssignedUserId = assignedUserId,
            ManagerEmployeeId = managerEmployeeId,
            IsOpen = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };
    }

    public void Decide(Guid decidedByUserId, bool approved, string? rejectReason)
    {
        if (!IsOpen)
        {
            throw new InvalidOperationException("Task is already decided.");
        }

        if (!approved && string.IsNullOrWhiteSpace(rejectReason))
        {
            throw new ArgumentException("Reject reason is required.");
        }

        IsOpen = false;
        Approved = approved;
        RejectReason = approved ? null : rejectReason!.Trim();
        DecidedByUserId = decidedByUserId;
        DecidedAt = DateTimeOffset.UtcNow;
    }

    public void CloseCancelled()
    {
        if (!IsOpen)
        {
            return;
        }

        IsOpen = false;
        Approved = null;
        RejectReason = "Cancelled";
        DecidedAt = DateTimeOffset.UtcNow;
    }
}
