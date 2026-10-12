namespace Kadr.Platform.Application.Payroll.Calculation;

/// <summary>Mutable scratchpad for a single pure calculation pass. No I/O.</summary>
public sealed class PayrollCalcContext
{
    public PayrollCalcContext(PayrollCalcInput input, RulesSnapshot rules)
    {
        Input = input;
        Rules = rules;
        Rounding = rules.GetRoundingPolicy();
    }

    public PayrollCalcInput Input { get; }
    public RulesSnapshot Rules { get; }
    public RoundingPolicy Rounding { get; }

    public List<PayrollCalcLine> Lines { get; } = [];
    public List<PayrollCalcWarning> Warnings { get; } = [];

    /// <summary>Days used as the proration denominator (from working_days_basis).</summary>
    public int DaysBasis { get; set; }

    /// <summary>Prorated basic total for the period (sum of segment basics).</summary>
    public decimal ProratedBasicTotal { get; set; }

    /// <summary>Monthly basic of the segment covering period end (for daily-rate deductions / OT).</summary>
    public decimal PeriodEndMonthlyBasic { get; set; }

    public decimal TaxableEarnings { get; set; }
    public decimal InsurableEarnings { get; set; }

    public decimal Gross { get; set; }
    public decimal InsurableWage { get; set; }
    public decimal EmployeeInsurance { get; set; }
    public decimal EmployerInsurance { get; set; }
    public decimal EmergencyEmployerCost { get; set; }
    public decimal TaxableIncome { get; set; }
    public decimal IncomeTax { get; set; }
    public decimal OtherDeductions { get; set; }
    public decimal Net { get; set; }
    public decimal EmployerCostTotal { get; set; }
    public bool DeductionsExceedGross { get; set; }
    public decimal ExcessDeductions { get; set; }

    public void AddLine(
        string lineType,
        string? componentKey,
        string labelAr,
        string labelEn,
        decimal amount,
        string? explanation = null)
    {
        var rounded = PayrollMoney.Round(amount, Rounding);
        Lines.Add(new PayrollCalcLine(lineType, componentKey, labelAr, labelEn, rounded, explanation));
    }

    public void AddWarning(string code, string messageAr, string messageEn)
    {
        if (Warnings.Any(w => w.Code == code))
        {
            return;
        }

        Warnings.Add(new PayrollCalcWarning(code, messageAr, messageEn));
    }

    public decimal SumLines(string lineType) =>
        Lines.Where(l => l.LineType == lineType).Sum(l => l.Amount);
}
