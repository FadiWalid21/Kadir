namespace Kadr.Platform.Application.Payroll.Calculation;

/// <summary>
/// Deterministic pipeline over <see cref="IPayrollCalcStep"/> implementations.
/// Order matches modules/07 §4 (step 9 is an F23 stub).
/// </summary>
public sealed class PayrollCalculationEngine : IPayrollCalculationEngine
{
    private readonly IReadOnlyList<IPayrollCalcStep> _steps;

    public PayrollCalculationEngine()
        : this(CreateDefaultSteps())
    {
    }

    public PayrollCalculationEngine(IReadOnlyList<IPayrollCalcStep> steps)
    {
        _steps = steps;
    }

    public static IReadOnlyList<IPayrollCalcStep> CreateDefaultSteps() =>
    [
        new SalaryProrationStep(),
        new InputsStep(),
        new EarningsStep(),
        new UnpaidDeductionsStep(),
        new InsurableWageStep(),
        new InsuranceContributionsStep(),
        new MartyrsFundStep(),
        new IncomeTaxStep(),
        new OtherDeductionsStubStep(), // F23: loans / advances / penalties
        new NetPayStep(),
        new RoundingTotalsStep(),
    ];

    public PayrollCalcResult Calculate(PayrollCalcInput input, RulesSnapshot rules)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(rules);

        var context = new PayrollCalcContext(input, rules);
        foreach (var step in _steps)
        {
            step.Execute(context);
        }

        return new PayrollCalcResult(
            Gross: context.Gross,
            InsurableWage: context.InsurableWage,
            EmployeeInsurance: context.EmployeeInsurance,
            EmployerInsurance: context.EmployerInsurance,
            TaxableIncome: context.TaxableIncome,
            IncomeTax: context.IncomeTax,
            OtherDeductions: context.OtherDeductions,
            Net: context.Net,
            EmployerCostTotal: context.EmployerCostTotal,
            DeductionsExceedGross: context.DeductionsExceedGross,
            ExcessDeductions: context.ExcessDeductions,
            RoundingPolicy: context.Rounding,
            Lines: context.Lines.ToArray(),
            Warnings: context.Warnings.ToArray());
    }
}
