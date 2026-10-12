namespace Kadr.Platform.Application.Payroll.Calculation;

public enum SalaryComponentKind
{
    Earning = 0,
    Deduction = 1,
    EmployerCost = 2,
}

/// <summary>Employee facts for one payroll period. Pure data — no I/O.</summary>
public sealed record PayrollCalcInput(
    Guid EmployeeId,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    IReadOnlyList<SalarySegment> SalarySegments,
    InsuranceFacts Insurance,
    PeriodAttendanceInputs Inputs);

/// <summary>Effective-dated salary slice that overlaps the period.</summary>
public sealed record SalarySegment(
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    decimal BasicAmount,
    IReadOnlyList<SalaryComponentFact> Components,
    bool BasicIsTaxable = true,
    bool BasicIsInsurable = true);

public sealed record SalaryComponentFact(
    string ComponentKey,
    string LabelAr,
    string LabelEn,
    decimal Amount,
    bool IsTaxable,
    bool IsInsurable,
    SalaryComponentKind Kind);

public sealed record InsuranceFacts(
    bool IsInsured,
    decimal? InsurableWageOverride);

public sealed record PeriodAttendanceInputs(
    int AbsenceDays,
    int UnpaidLeaveDays,
    int LateMinutes,
    decimal OvertimeHoursDay,
    decimal OvertimeHoursNight,
    decimal OvertimeHoursHoliday);

public sealed record TaxBracket(decimal From, decimal? To, decimal RatePercent);

public sealed record PayrollCalcLine(
    string LineType,
    string? ComponentKey,
    string LabelAr,
    string LabelEn,
    decimal Amount,
    string? Explanation);

public sealed record PayrollCalcWarning(string Code, string MessageAr, string MessageEn);

public sealed record PayrollCalcResult(
    decimal Gross,
    decimal InsurableWage,
    decimal EmployeeInsurance,
    decimal EmployerInsurance,
    decimal TaxableIncome,
    decimal IncomeTax,
    decimal OtherDeductions,
    decimal Net,
    decimal EmployerCostTotal,
    bool DeductionsExceedGross,
    decimal ExcessDeductions,
    RoundingPolicy RoundingPolicy,
    IReadOnlyList<PayrollCalcLine> Lines,
    IReadOnlyList<PayrollCalcWarning> Warnings);
