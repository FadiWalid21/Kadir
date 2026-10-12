namespace Kadr.Platform.Application.Payroll.Calculation;

/// <summary>One pipeline step of the pure payroll calculation engine.</summary>
public interface IPayrollCalcStep
{
    string Name { get; }

    void Execute(PayrollCalcContext context);
}
