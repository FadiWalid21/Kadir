using Kadr.SharedKernel.Domain;
using Kadr.SharedKernel.Ids;

namespace Kadr.Platform.Domain.Workflows;

public sealed class ApprovalWorkflowStep : Entity, ITenantOwned
{
    private ApprovalWorkflowStep()
    {
    }

    public Guid TenantId { get; private set; }
    public Guid WorkflowId { get; private set; }
    public int StepOrder { get; private set; }
    public ApproverKind ApproverKind { get; private set; }
    public Guid? RoleId { get; private set; }
    public Guid? UserId { get; private set; }
    public bool IsFinal { get; private set; }

    public static ApprovalWorkflowStep Create(
        Guid tenantId,
        Guid workflowId,
        int stepOrder,
        ApproverKind kind,
        Guid? roleId,
        Guid? userId,
        bool isFinal)
    {
        if (stepOrder < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(stepOrder));
        }

        if (kind == ApproverKind.Role && roleId is null)
        {
            throw new ArgumentException("Role is required for role approver steps.");
        }

        if (kind == ApproverKind.User && userId is null)
        {
            throw new ArgumentException("User is required for user approver steps.");
        }

        return new ApprovalWorkflowStep
        {
            Id = GuidV7.NewGuid(),
            TenantId = tenantId,
            WorkflowId = workflowId,
            StepOrder = stepOrder,
            ApproverKind = kind,
            RoleId = kind == ApproverKind.Role ? roleId : null,
            UserId = kind == ApproverKind.User ? userId : null,
            IsFinal = isFinal,
        };
    }
}
