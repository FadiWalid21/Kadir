namespace Kadr.Platform.Application.Payroll;

public sealed record PayrollRuleListItemDto(
    string RuleKey,
    string ValueType,
    string ValueJson,
    DateOnly EffectiveFrom,
    bool IsCustomized,
    bool IsVerified,
    bool DiffersFromOfficialDefault,
    bool UpdateAvailable,
    string? OfficialDefaultJson,
    string Reason,
    Guid Id);

public sealed record PayrollRulesListDto(
    IReadOnlyList<PayrollRuleListItemDto> Items,
    bool RulesConfirmedForLock,
    DateTimeOffset? RulesConfirmedAt,
    string? RulesConfirmedReason);

public sealed record PayrollRuleHistoryItemDto(
    Guid Id,
    string ValueJson,
    DateOnly EffectiveFrom,
    bool IsCustomized,
    bool IsVerified,
    string Reason,
    DateTimeOffset CreatedAt,
    Guid? CreatedBy);

public sealed record UpsertPayrollRuleRequest(
    string ValueJson,
    DateOnly EffectiveFrom,
    string Reason);

public sealed record ConfirmPayrollRulesRequest(string Reason);

public sealed record ConfirmPayrollRulesDto(
    bool RulesConfirmedForLock,
    DateTimeOffset RulesConfirmedAt,
    string Reason);
