namespace Kadr.Platform.Application.Payroll;

public sealed record EmployeeSalaryComponentDto(
    Guid Id,
    Guid PayComponentId,
    string PayComponentKey,
    string PayComponentNameAr,
    string PayComponentNameEn,
    decimal AmountOrRate,
    bool IsTaxable,
    bool IsInsurable);

public sealed record EmployeeSalaryDto(
    Guid Id,
    Guid EmployeeId,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    string Currency,
    decimal BasicAmount,
    string ChangeReason,
    IReadOnlyList<EmployeeSalaryComponentDto> Components);

public sealed record EmployeeSalaryListItemDto(
    Guid Id,
    Guid EmployeeId,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    string Currency,
    decimal BasicAmount,
    decimal GrossAmount,
    string ChangeReason,
    int ComponentCount);

public sealed record PagedEmployeeSalariesDto(
    IReadOnlyList<EmployeeSalaryListItemDto> Items,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record CreateEmployeeSalaryComponentInput(
    Guid PayComponentId,
    decimal AmountOrRate);

public sealed record CreateEmployeeSalaryRequest(
    DateOnly EffectiveFrom,
    string? Currency,
    decimal BasicAmount,
    string ChangeReason,
    IReadOnlyList<CreateEmployeeSalaryComponentInput>? Components);
