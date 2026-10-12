namespace Kadr.Platform.Application.Payroll;

public sealed record EmployeeInsuranceProfileDto(
    Guid Id,
    Guid EmployeeId,
    bool IsInsured,
    string? InsuranceNumber,
    DateOnly? InsuranceStartDate,
    decimal? InsurableWageOverride);

public sealed record UpsertEmployeeInsuranceRequest(
    bool IsInsured,
    string? InsuranceNumber,
    DateOnly? InsuranceStartDate,
    decimal? InsurableWageOverride);
