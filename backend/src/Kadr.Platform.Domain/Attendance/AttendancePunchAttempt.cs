using Kadr.SharedKernel.Domain;
using Kadr.SharedKernel.Ids;

namespace Kadr.Platform.Domain.Attendance;

/// <summary>Rejected punch attempt. Never stores coordinates.</summary>
public sealed class AttendancePunchAttempt : Entity, ITenantOwned
{
    private AttendancePunchAttempt()
    {
    }

    public Guid TenantId { get; private set; }
    public Guid EmployeeId { get; private set; }
    public Guid? BranchId { get; private set; }
    public string ReasonCode { get; private set; } = string.Empty;
    public double? AccuracyM { get; private set; }
    public double? DistanceM { get; private set; }
    public Guid? ClientRequestId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid? CreatedBy { get; private set; }

    public static AttendancePunchAttempt Create(
        Guid tenantId,
        Guid employeeId,
        Guid? branchId,
        string reasonCode,
        double? accuracyM,
        double? distanceM,
        Guid? clientRequestId,
        Guid? createdBy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reasonCode);

        return new AttendancePunchAttempt
        {
            Id = GuidV7.NewGuid(),
            TenantId = tenantId,
            EmployeeId = employeeId,
            BranchId = branchId,
            ReasonCode = reasonCode.Trim(),
            AccuracyM = accuracyM,
            DistanceM = distanceM,
            ClientRequestId = clientRequestId,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = createdBy,
        };
    }
}
