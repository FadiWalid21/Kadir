using Kadr.SharedKernel.Domain;
using Kadr.SharedKernel.Ids;

namespace Kadr.Platform.Domain.Attendance;

public sealed class AttendancePunch : AggregateRoot, ITenantOwned
{
    private AttendancePunch()
    {
    }

    public Guid TenantId { get; private set; }
    public Guid EmployeeId { get; private set; }
    public Guid BranchId { get; private set; }
    public PunchKind Kind { get; private set; }
    public DateTimeOffset PunchedAt { get; private set; }
    public DateOnly WorkDate { get; private set; }
    public Guid? ShiftId { get; private set; }
    public PunchSource Source { get; private set; }
    public double? AccuracyM { get; private set; }
    public double? DistanceM { get; private set; }
    public bool IsInside { get; private set; }
    public string? OutsideReason { get; private set; }
    public double? Latitude { get; private set; }
    public double? Longitude { get; private set; }
    public Guid ClientRequestId { get; private set; }
    public Guid? CreatedBy { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static AttendancePunch CreateAccepted(
        Guid tenantId,
        Guid employeeId,
        Guid branchId,
        PunchKind kind,
        DateTimeOffset punchedAtUtc,
        DateOnly workDate,
        Guid? shiftId,
        PunchSource source,
        double accuracyM,
        double distanceM,
        bool isInside,
        string? outsideReason,
        double latitude,
        double longitude,
        Guid clientRequestId,
        Guid? createdBy)
    {
        if (clientRequestId == Guid.Empty)
        {
            throw new ArgumentException("Client request id is required.", nameof(clientRequestId));
        }

        return new AttendancePunch
        {
            Id = GuidV7.NewGuid(),
            TenantId = tenantId,
            EmployeeId = employeeId,
            BranchId = branchId,
            Kind = kind,
            PunchedAt = punchedAtUtc.ToUniversalTime(),
            WorkDate = workDate,
            ShiftId = shiftId,
            Source = source,
            AccuracyM = accuracyM,
            DistanceM = distanceM,
            IsInside = isInside,
            OutsideReason = string.IsNullOrWhiteSpace(outsideReason) ? null : outsideReason.Trim(),
            Latitude = latitude,
            Longitude = longitude,
            ClientRequestId = clientRequestId,
            CreatedBy = createdBy,
            CreatedAt = DateTimeOffset.UtcNow,
        };
    }
}
