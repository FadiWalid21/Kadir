namespace Kadr.Platform.Application.Payroll.Calculation;

/// <summary>
/// Pure payroll calculation: (employee period input, rules snapshot) → lines and totals.
/// No DbContext, no clock, no I/O.
/// </summary>
public interface IPayrollCalculationEngine
{
    PayrollCalcResult Calculate(PayrollCalcInput input, RulesSnapshot rules);
}
