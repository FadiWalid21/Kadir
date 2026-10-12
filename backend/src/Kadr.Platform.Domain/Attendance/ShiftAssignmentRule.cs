using Kadr.SharedKernel.Domain;
using Kadr.SharedKernel.Ids;

namespace Kadr.Platform.Domain.Attendance;

/// <summary>
/// Standing shift rule for a target (employee / job title / department / branch).
/// Day-level <see cref="ShiftAssignment"/> overrides this at calculation time.
/// </summary>
public sealed class ShiftAssignmentRule : Entity, ITenantOwned
{
    private ShiftAssignmentRule()
    {
    }

    public Guid TenantId { get; private set; }
    public ShiftAssignmentTargetType TargetType { get; private set; }
    public Guid TargetId { get; private set; }
    public Guid? ShiftId { get; private set; }
    public bool IsDayOff { get; private set; }
    public DateOnly EffectiveFrom { get; private set; }
    public DateOnly? EffectiveTo { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid? CreatedBy { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public Guid? UpdatedBy { get; private set; }

    public static ShiftAssignmentRule Create(
        Guid tenantId,
        ShiftAssignmentTargetType targetType,
        Guid targetId,
        Guid? shiftId,
        bool isDayOff,
        DateOnly effectiveFrom,
        DateOnly? effectiveTo,
        Guid? createdBy)
    {
        if (!isDayOff && shiftId is null)
        {
            throw new ArgumentException("Shift is required unless the rule is day off.");
        }

        if (effectiveTo is not null && effectiveTo < effectiveFrom)
        {
            throw new ArgumentException("EffectiveTo must be on or after EffectiveFrom.");
        }

        return new ShiftAssignmentRule
        {
            Id = GuidV7.NewGuid(),
            TenantId = tenantId,
            TargetType = targetType,
            TargetId = targetId,
            ShiftId = isDayOff ? null : shiftId,
            IsDayOff = isDayOff,
            EffectiveFrom = effectiveFrom,
            EffectiveTo = effectiveTo,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = createdBy,
        };
    }

    public void Update(
        Guid? shiftId,
        bool isDayOff,
        DateOnly effectiveFrom,
        DateOnly? effectiveTo,
        Guid? updatedBy)
    {
        if (!isDayOff && shiftId is null)
        {
            throw new ArgumentException("Shift is required unless the rule is day off.");
        }

        if (effectiveTo is not null && effectiveTo < effectiveFrom)
        {
            throw new ArgumentException("EffectiveTo must be on or after EffectiveFrom.");
        }

        IsDayOff = isDayOff;
        ShiftId = isDayOff ? null : shiftId;
        EffectiveFrom = effectiveFrom;
        EffectiveTo = effectiveTo;
        UpdatedAt = DateTimeOffset.UtcNow;
        UpdatedBy = updatedBy;
    }

    public void End(DateOnly effectiveTo, Guid? updatedBy)
    {
        if (effectiveTo < EffectiveFrom)
        {
            throw new ArgumentException("EffectiveTo must be on or after EffectiveFrom.");
        }

        EffectiveTo = effectiveTo;
        UpdatedAt = DateTimeOffset.UtcNow;
        UpdatedBy = updatedBy;
    }

    public bool Covers(DateOnly date) =>
        date >= EffectiveFrom && (EffectiveTo is null || date <= EffectiveTo);

    /// <summary>Lower number = higher precedence among standing rules.</summary>
    public static int PrecedenceRank(ShiftAssignmentTargetType type) => type switch
    {
        ShiftAssignmentTargetType.Employee => 0,
        ShiftAssignmentTargetType.JobTitle => 1,
        ShiftAssignmentTargetType.Department => 2,
        ShiftAssignmentTargetType.Branch => 3,
        _ => 99,
    };
}
