namespace Kadr.Platform.Application.Attendance;

public sealed record ShiftAssignmentRuleDto(
    Guid Id,
    string TargetType,
    Guid TargetId,
    string? TargetName,
    Guid? ShiftId,
    string? ShiftNameAr,
    string? ShiftNameEn,
    bool IsDayOff,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo);

public sealed record PagedShiftAssignmentRulesDto(
    IReadOnlyList<ShiftAssignmentRuleDto> Items,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record CreateShiftAssignmentRulesRequest(
    string TargetType,
    IReadOnlyList<Guid> TargetIds,
    Guid? ShiftId,
    bool IsDayOff,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    bool ConfirmRecalc);

public sealed record UpdateShiftAssignmentRuleRequest(
    Guid? ShiftId,
    bool IsDayOff,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    bool ConfirmRecalc);

public sealed record EndShiftAssignmentRuleRequest(DateOnly EffectiveTo);

public sealed record PreviewShiftAssignmentRulesRequest(
    string TargetType,
    IReadOnlyList<Guid> TargetIds,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo);

public sealed record PreviewShiftAssignmentRulesDto(
    int AffectedEmployeeCount,
    int OverrideCount,
    string Note);
