using Kadr.Platform.Domain.Leave;
using Kadr.Platform.Domain.Workflows;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kadr.Infrastructure.Persistence.Configurations;

public sealed class LeaveRequestConfiguration : IEntityTypeConfiguration<LeaveRequest>
{
    public void Configure(EntityTypeBuilder<LeaveRequest> builder)
    {
        builder.ToTable("leave_requests");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Days).HasPrecision(8, 2);
        builder.Property(x => x.Reason).HasMaxLength(2000);
        builder.Property(x => x.RejectReason).HasMaxLength(2000);
        builder.HasIndex(x => new { x.TenantId, x.EmployeeId, x.Status });
        builder.HasIndex(x => new { x.TenantId, x.StartDate, x.EndDate });
        builder.HasIndex(x => new { x.TenantId, x.WorkflowInstanceId });
    }
}

public sealed class HourlyPermissionRequestConfiguration : IEntityTypeConfiguration<HourlyPermissionRequest>
{
    public void Configure(EntityTypeBuilder<HourlyPermissionRequest> builder)
    {
        builder.ToTable("hourly_permission_requests");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Hours).HasPrecision(8, 2);
        builder.Property(x => x.Reason).HasMaxLength(2000);
        builder.Property(x => x.RejectReason).HasMaxLength(2000);
        builder.HasIndex(x => new { x.TenantId, x.EmployeeId, x.Status });
        builder.HasIndex(x => new { x.TenantId, x.WorkDate });
    }
}

public sealed class LeaveBalanceConfiguration : IEntityTypeConfiguration<LeaveBalance>
{
    public void Configure(EntityTypeBuilder<LeaveBalance> builder)
    {
        builder.ToTable("leave_balances");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Entitled).HasPrecision(8, 2);
        builder.Property(x => x.Accrued).HasPrecision(8, 2);
        builder.Property(x => x.Used).HasPrecision(8, 2);
        builder.Property(x => x.Adjustment).HasPrecision(8, 2);
        builder.Property(x => x.CarriedIn).HasPrecision(8, 2);
        builder.Ignore(x => x.Remaining);
        builder.HasIndex(x => new { x.TenantId, x.EmployeeId, x.LeaveTypeId, x.Year }).IsUnique();
    }
}

public sealed class LeaveBalanceAdjustmentConfiguration : IEntityTypeConfiguration<LeaveBalanceAdjustment>
{
    public void Configure(EntityTypeBuilder<LeaveBalanceAdjustment> builder)
    {
        builder.ToTable("leave_balance_adjustments");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.DeltaDays).HasPrecision(8, 2);
        builder.Property(x => x.Reason).HasMaxLength(1000);
        builder.HasIndex(x => new { x.TenantId, x.EmployeeId, x.Year });
    }
}

public sealed class ApprovalWorkflowConfiguration : IEntityTypeConfiguration<ApprovalWorkflow>
{
    public void Configure(EntityTypeBuilder<ApprovalWorkflow> builder)
    {
        builder.ToTable("approval_workflows");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.NameAr).HasMaxLength(200);
        builder.Property(x => x.NameEn).HasMaxLength(200);
        builder.HasIndex(x => new { x.TenantId, x.RequestType, x.IsDefault });
        builder.HasIndex(x => new { x.TenantId, x.IsActive });
    }
}

public sealed class ApprovalWorkflowStepConfiguration : IEntityTypeConfiguration<ApprovalWorkflowStep>
{
    public void Configure(EntityTypeBuilder<ApprovalWorkflowStep> builder)
    {
        builder.ToTable("approval_workflow_steps");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.TenantId, x.WorkflowId, x.StepOrder }).IsUnique();
        builder.HasOne<ApprovalWorkflow>().WithMany().HasForeignKey(x => x.WorkflowId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class WorkflowInstanceConfiguration : IEntityTypeConfiguration<WorkflowInstance>
{
    public void Configure(EntityTypeBuilder<WorkflowInstance> builder)
    {
        builder.ToTable("workflow_instances");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.TenantId, x.RequestType, x.Status });
        builder.HasIndex(x => new { x.TenantId, x.SubjectId });
    }
}

public sealed class WorkflowTaskConfiguration : IEntityTypeConfiguration<WorkflowTask>
{
    public void Configure(EntityTypeBuilder<WorkflowTask> builder)
    {
        builder.ToTable("workflow_tasks");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.RejectReason).HasMaxLength(2000);
        builder.HasIndex(x => new { x.TenantId, x.IsOpen, x.AssignedUserId });
        builder.HasIndex(x => new { x.TenantId, x.IsOpen, x.ManagerEmployeeId });
        builder.HasIndex(x => new { x.TenantId, x.IsOpen, x.RoleId });
        builder.HasIndex(x => new { x.TenantId, x.InstanceId, x.StepOrder });
    }
}
