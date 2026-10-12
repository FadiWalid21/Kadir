namespace Kadr.Platform.Application.Payroll;

public sealed record CreatePayrollRunRequest(DateOnly PeriodStart, DateOnly PeriodEnd);

public sealed record ReopenPayrollRunRequest(string Reason);

public sealed record HoldPayrollEntryRequest(string? Reason);

public sealed record PayrollRunListItemDto(
    Guid Id,
    Guid LegalEntityId,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    string Status,
    int WarningCount,
    int Headcount,
    decimal? TotalGross,
    decimal? TotalNet,
    decimal? TotalEmployerCost,
    Guid CreatedBy,
    Guid? ApprovedBy,
    DateTimeOffset? LockedAt,
    Guid? ReopenedFromRunId,
    uint Version,
    DateTimeOffset CreatedAt);

public sealed record PagedPayrollRunsDto(
    IReadOnlyList<PayrollRunListItemDto> Items,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record PayrollRunDetailDto(
    Guid Id,
    Guid LegalEntityId,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    string Status,
    string RulesSnapshotJson,
    int WarningCount,
    int Headcount,
    decimal? TotalGross,
    decimal? TotalNet,
    decimal? TotalEmployerCost,
    Guid CreatedBy,
    Guid? ApprovedBy,
    DateTimeOffset? LockedAt,
    Guid? ReopenedFromRunId,
    uint Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record PayrollEntryListItemDto(
    Guid Id,
    Guid RunId,
    Guid EmployeeId,
    string EmployeeNumber,
    string FullNameAr,
    string? FullNameEn,
    Guid? DepartmentId,
    decimal Gross,
    decimal Net,
    decimal EmployerCostTotal,
    int WarningCount,
    bool IsHeld,
    string? HoldReason);

public sealed record PagedPayrollEntriesDto(
    IReadOnlyList<PayrollEntryListItemDto> Items,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record PayrollEntryLineDto(
    Guid Id,
    string LineType,
    string? ComponentKey,
    string LabelAr,
    string LabelEn,
    decimal Amount,
    string? CalcExplanationJson,
    int SortOrder);

public sealed record PayrollEntryDetailDto(
    Guid Id,
    Guid RunId,
    Guid EmployeeId,
    string EmployeeNumber,
    string FullNameAr,
    string? FullNameEn,
    string SalarySnapshotJson,
    string InputsJson,
    decimal Gross,
    decimal InsurableWage,
    decimal EmployeeInsurance,
    decimal EmployerInsurance,
    decimal TaxableIncome,
    decimal IncomeTax,
    decimal OtherDeductions,
    decimal Net,
    decimal EmployerCostTotal,
    bool IsHeld,
    string? HoldReason,
    int WarningCount,
    string? WarningsJson,
    IReadOnlyList<PayrollEntryLineDto> Lines);
