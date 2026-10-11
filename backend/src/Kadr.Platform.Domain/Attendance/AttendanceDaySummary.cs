using Kadr.SharedKernel.Domain;
using Kadr.SharedKernel.Ids;

namespace Kadr.Platform.Domain.Attendance;

public sealed class AttendanceDaySummary : AggregateRoot, ITenantOwned
{
    private AttendanceDaySummary()
    {
    }

    public Guid TenantId { get; private set; }
    public Guid EmployeeId { get; private set; }
    public DateOnly WorkDate { get; private set; }
    public Guid? ShiftId { get; private set; }
    public DateTimeOffset? FirstInAt { get; private set; }
    public DateTimeOffset? LastOutAt { get; private set; }
    public int WorkedMinutes { get; private set; }
    public int LateMinutes { get; private set; }
    public int EarlyLeaveMinutes { get; private set; }
    public int OvertimeMinutes { get; private set; }
    public AttendanceDayStatus Status { get; private set; }
    public bool IsManualOverride { get; private set; }
    public DateTimeOffset? ClosedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static AttendanceDaySummary Create(
        Guid tenantId,
        Guid employeeId,
        DateOnly workDate,
        Guid? shiftId,
        AttendanceDayStatus status)
    {
        return new AttendanceDaySummary
        {
            Id = GuidV7.NewGuid(),
            TenantId = tenantId,
            EmployeeId = employeeId,
            WorkDate = workDate,
            ShiftId = shiftId,
            Status = status,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
    }

    public void ApplyComputed(
        Guid? shiftId,
        DateTimeOffset? firstIn,
        DateTimeOffset? lastOut,
        int workedMinutes,
        int lateMinutes,
        int earlyLeaveMinutes,
        int overtimeMinutes,
        AttendanceDayStatus status)
    {
        if (IsManualOverride)
        {
            return;
        }

        ShiftId = shiftId;
        FirstInAt = firstIn;
        LastOutAt = lastOut;
        WorkedMinutes = workedMinutes;
        LateMinutes = lateMinutes;
        EarlyLeaveMinutes = earlyLeaveMinutes;
        OvertimeMinutes = overtimeMinutes;
        Status = status;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void MarkAbsent()
    {
        if (IsManualOverride || Status is AttendanceDayStatus.Weekend or AttendanceDayStatus.Holiday or AttendanceDayStatus.Leave)
        {
            return;
        }

        Status = AttendanceDayStatus.Absent;
        ClosedAt = DateTimeOffset.UtcNow;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void SetManualOverride(AttendanceDayStatus status)
    {
        Status = status;
        IsManualOverride = true;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
