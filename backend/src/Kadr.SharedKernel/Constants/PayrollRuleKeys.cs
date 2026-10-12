namespace Kadr.SharedKernel.Constants;

/// <summary>Company-controlled payroll rule keys. Seed values are [VERIFY] until an accountant confirms.</summary>
public static class PayrollRuleKeys
{
    public const string InsuranceWageMin = "insurance.wage_min";
    public const string InsuranceWageMax = "insurance.wage_max";
    public const string InsuranceEmployeeRate = "insurance.employee_rate";
    public const string InsuranceEmployerRate = "insurance.employer_rate";
    public const string FundMartyrsRate = "fund.martyrs_rate";
    public const string FundEmergencyEmployerRate = "fund.emergency_employer_rate";
    public const string TaxPersonalExemptionAnnual = "tax.personal_exemption_annual";
    public const string TaxBrackets = "tax.brackets";
    public const string OvertimeMultiplierDay = "overtime.multiplier_day";
    public const string OvertimeMultiplierNight = "overtime.multiplier_night";
    public const string OvertimeMultiplierHoliday = "overtime.multiplier_holiday";
    public const string PayrollPeriodStartDay = "payroll.period_start_day";
    public const string PayrollWorkingDaysBasis = "payroll.working_days_basis";
    public const string PayrollSecondApprovalRequired = "payroll.second_approval_required";
    public const string PayrollRounding = "payroll.rounding";
    public const string PayrollTaxMethod = "payroll.tax_method";

    public static readonly IReadOnlyList<string> All =
    [
        InsuranceWageMin,
        InsuranceWageMax,
        InsuranceEmployeeRate,
        InsuranceEmployerRate,
        FundMartyrsRate,
        FundEmergencyEmployerRate,
        TaxPersonalExemptionAnnual,
        TaxBrackets,
        OvertimeMultiplierDay,
        OvertimeMultiplierNight,
        OvertimeMultiplierHoliday,
        PayrollPeriodStartDay,
        PayrollWorkingDaysBasis,
        PayrollSecondApprovalRequired,
        PayrollRounding,
        PayrollTaxMethod,
    ];
}
