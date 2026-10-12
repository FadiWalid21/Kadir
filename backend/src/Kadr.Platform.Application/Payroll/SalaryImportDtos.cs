namespace Kadr.Platform.Application.Payroll;

public sealed record SalaryImportPreviewErrorDto(int Row, string Field, string Code, string Message);

public sealed record SalaryImportCommitRowDto(
    Guid EmployeeId,
    string EmployeeNumber,
    DateOnly EffectiveFrom,
    string Currency,
    decimal BasicAmount,
    string ChangeReason,
    IReadOnlyList<CreateEmployeeSalaryComponentInput> Components);

public sealed record SalaryImportPreviewResultDto(
    int ValidCount,
    int ErrorCount,
    IReadOnlyList<SalaryImportPreviewErrorDto> Errors,
    IReadOnlyList<SalaryImportCommitRowDto> ValidRows);

public sealed record SalaryImportCommitResultDto(
    int ImportedCount,
    int SkippedCount,
    IReadOnlyList<SalaryImportPreviewErrorDto> Errors);
