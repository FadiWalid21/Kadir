namespace Kadr.Platform.Application.Payroll.Calculation;

public static class PayrollCalcLineTypes
{
    public const string Earning = "earning";
    public const string Deduction = "deduction";
    public const string EmployerCost = "employer_cost";
}

public static class PayrollCalcComponentKeys
{
    public const string BasicProrated = "basic_prorated";
    public const string OvertimeDay = "overtime_day";
    public const string OvertimeNight = "overtime_night";
    public const string OvertimeHoliday = "overtime_holiday";
    public const string UnpaidLeave = "unpaid_leave";
    public const string Absence = "absence";
    public const string Late = "late";
    public const string EmployeeInsurance = "employee_insurance";
    public const string EmployerInsurance = "employer_insurance";
    public const string EmergencyFundEmployer = "emergency_fund_employer";
    public const string MartyrsFund = "martyrs_fund";
    public const string IncomeTax = "income_tax";
}
