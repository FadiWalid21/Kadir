using System.Text.Json;
using Kadr.Platform.Application.Payroll.Calculation;
using Kadr.SharedKernel.Constants;

namespace Kadr.Platform.Tests.Payroll;

/// <summary>
/// Pure engine tests (modules/07 §4, §8 calc ACs). Statutory figures marked [VERIFY].
/// </summary>
public sealed class PayrollCalculationEngineTests
{
    private readonly IPayrollCalculationEngine _engine = new PayrollCalculationEngine();

    // [VERIFY] BRD §3.1 / EgyptStatutoryRuleCatalog seed figures
    private const decimal WageMin = 2700m;
    private const decimal WageMax = 16700m;
    private const decimal EmployeeInsuranceRate = 11m;
    private const decimal EmployerInsuranceRate = 18.75m;
    private const decimal MartyrsRate = 0.05m;
    private const decimal EmergencyEmployerRate = 1m;
    private const decimal PersonalExemptionAnnual = 20000m;

    [Fact]
    public void Insurable_wage_clamped_below_min()
    {
        var input = BaseInput(basic: 2000m, isInsurable: true);
        var rules = DefaultRules();

        var result = _engine.Calculate(input, rules);

        Assert.Equal(WageMin, result.InsurableWage);
        // [VERIFY] 2700 * 11%
        Assert.Equal(PayrollMoney.Round(WageMin * EmployeeInsuranceRate / 100m, result.RoundingPolicy), result.EmployeeInsurance);
        // [VERIFY] 2700 * 18.75%
        Assert.Equal(PayrollMoney.Round(WageMin * EmployerInsuranceRate / 100m, result.RoundingPolicy), result.EmployerInsurance);
    }

    [Fact]
    public void Insurable_wage_clamped_above_max()
    {
        var input = BaseInput(basic: 20000m, isInsurable: true);
        var rules = DefaultRules();

        var result = _engine.Calculate(input, rules);

        Assert.Equal(WageMax, result.InsurableWage);
        // [VERIFY] 16700 * 11%
        Assert.Equal(PayrollMoney.Round(WageMax * EmployeeInsuranceRate / 100m, result.RoundingPolicy), result.EmployeeInsurance);
        // [VERIFY] 16700 * 18.75%
        Assert.Equal(PayrollMoney.Round(WageMax * EmployerInsuranceRate / 100m, result.RoundingPolicy), result.EmployerInsurance);
    }

    [Fact]
    public void Empty_tax_brackets_yield_warning_and_zero_tax()
    {
        var input = BaseInput(basic: 5000m, isInsurable: true);
        var rules = DefaultRules(); // empty brackets

        var result = _engine.Calculate(input, rules);

        Assert.Equal(0m, result.IncomeTax);
        Assert.Contains(result.Warnings, w => w.Code == PayrollCalcWarningCodes.TaxEmptyBrackets);
    }

    [Fact]
    public void Mid_period_salary_proration_produces_two_explanation_lines()
    {
        var periodStart = new DateOnly(2026, 3, 1);
        var periodEnd = new DateOnly(2026, 3, 30); // 30 days, matches fixed_30
        var segments = new[]
        {
            new SalarySegment(
                EffectiveFrom: periodStart,
                EffectiveTo: new DateOnly(2026, 3, 15),
                BasicAmount: 3000m,
                Components: []),
            new SalarySegment(
                EffectiveFrom: new DateOnly(2026, 3, 16),
                EffectiveTo: null,
                BasicAmount: 6000m,
                Components: []),
        };

        var input = new PayrollCalcInput(
            EmployeeId: Guid.Parse("22222222-2222-2222-2222-222222222222"),
            PeriodStart: periodStart,
            PeriodEnd: periodEnd,
            SalarySegments: segments,
            Insurance: new InsuranceFacts(IsInsured: false, InsurableWageOverride: null),
            Inputs: new PeriodAttendanceInputs(
                AbsenceDays: 0,
                UnpaidLeaveDays: 0,
                LateMinutes: 0,
                OvertimeHoursDay: 0,
                OvertimeHoursNight: 0,
                OvertimeHoursHoliday: 0));

        var result = _engine.Calculate(input, DefaultRules());

        var prorationLines = result.Lines
            .Where(l => l.ComponentKey == PayrollCalcComponentKeys.BasicProrated)
            .ToList();
        Assert.Equal(2, prorationLines.Count);
        Assert.All(prorationLines, l => Assert.False(string.IsNullOrWhiteSpace(l.Explanation)));
        // 15/30 * 3000 + 15/30 * 6000 = 1500 + 3000
        Assert.Equal(4500m, prorationLines.Sum(l => l.Amount));
    }

    [Fact]
    public void Unpaid_leave_day_creates_explicit_deduction_line()
    {
        var input = BaseInput(basic: 3000m, isInsurable: false) with
        {
            Inputs = new PeriodAttendanceInputs(0, UnpaidLeaveDays: 1, 0, 0, 0, 0),
        };

        var result = _engine.Calculate(input, DefaultRules());

        var unpaid = Assert.Single(result.Lines, l => l.ComponentKey == PayrollCalcComponentKeys.UnpaidLeave);
        Assert.Equal(PayrollCalcLineTypes.Deduction, unpaid.LineType);
        // fixed_30: 3000 / 30 * 1
        Assert.Equal(100m, unpaid.Amount);
    }

    [Fact]
    public void Same_input_twice_is_deterministic()
    {
        var input = BaseInput(basic: 5000m, isInsurable: true) with
        {
            Inputs = new PeriodAttendanceInputs(0, 0, 0, OvertimeHoursDay: 2, 0, 0),
        };
        var rules = DefaultRules(otDay: 1.5m);

        var a = _engine.Calculate(input, rules);
        var b = _engine.Calculate(input, rules);

        Assert.Equal(a.Gross, b.Gross);
        Assert.Equal(a.Net, b.Net);
        Assert.Equal(a.IncomeTax, b.IncomeTax);
        Assert.Equal(a.InsurableWage, b.InsurableWage);
        Assert.Equal(a.Lines.Count, b.Lines.Count);
        Assert.Equal(
            a.Lines.Select(l => (l.LineType, l.ComponentKey, l.Amount)).ToArray(),
            b.Lines.Select(l => (l.LineType, l.ComponentKey, l.Amount)).ToArray());
        Assert.Equal(a.Warnings.Select(w => w.Code).ToArray(), b.Warnings.Select(w => w.Code).ToArray());
    }

    [Fact]
    public void Net_never_negative_when_deductions_exceed_gross()
    {
        // High unpaid leave relative to a tiny salary segment forces deductions > gross path via insurance + martyrs + heavy unpaid.
        var input = BaseInput(basic: 3000m, isInsurable: true) with
        {
            Inputs = new PeriodAttendanceInputs(0, UnpaidLeaveDays: 40, 0, 0, 0, 0),
        };
        var rules = DefaultRules();

        var result = _engine.Calculate(input, rules);

        Assert.True(result.Net >= 0m);
        Assert.True(result.DeductionsExceedGross);
        Assert.True(result.ExcessDeductions > 0m);
        Assert.Contains(result.Warnings, w => w.Code == PayrollCalcWarningCodes.NetDeductionsExceedGross);
    }

    [Fact]
    public void Property_employee_deductions_plus_net_approximate_gross()
    {
        var input = BaseInput(basic: 8000m, isInsurable: true) with
        {
            Inputs = new PeriodAttendanceInputs(1, 1, LateMinutes: 30, OvertimeHoursDay: 3, 1, 0),
        };
        var rules = DefaultRules(otDay: 1.5m, otNight: 2m);

        var result = _engine.Calculate(input, rules);

        var employeeDeductions = result.Lines
            .Where(l => l.LineType == PayrollCalcLineTypes.Deduction)
            .Sum(l => l.Amount);

        var lhs = employeeDeductions + result.Net;
        var diff = Math.Abs(lhs - result.Gross);
        Assert.True(diff <= 0.02m, $"deductions ({employeeDeductions}) + net ({result.Net}) vs gross ({result.Gross}), diff={diff}");
    }

    [Fact]
    public void Null_ot_multipliers_warn_and_zero_ot_amount()
    {
        var input = BaseInput(basic: 3000m, isInsurable: false) with
        {
            Inputs = new PeriodAttendanceInputs(0, 0, 0, OvertimeHoursDay: 5, 0, 0),
        };

        var result = _engine.Calculate(input, DefaultRules()); // null OT multipliers

        Assert.DoesNotContain(result.Lines, l => l.ComponentKey == PayrollCalcComponentKeys.OvertimeDay && l.Amount > 0);
        Assert.Contains(result.Warnings, w => w.Code == PayrollCalcWarningCodes.OvertimeMissingMultipliers);
    }

    private static PayrollCalcInput BaseInput(decimal basic, bool isInsurable)
    {
        var periodStart = new DateOnly(2026, 3, 1);
        var periodEnd = new DateOnly(2026, 3, 30);
        return new PayrollCalcInput(
            EmployeeId: Guid.Parse("11111111-1111-1111-1111-111111111111"),
            PeriodStart: periodStart,
            PeriodEnd: periodEnd,
            SalarySegments:
            [
                new SalarySegment(
                    EffectiveFrom: periodStart,
                    EffectiveTo: null,
                    BasicAmount: basic,
                    Components:
                    [
                        new SalaryComponentFact(
                            ComponentKey: "housing",
                            LabelAr: "بدل سكن",
                            LabelEn: "Housing",
                            Amount: 0m,
                            IsTaxable: true,
                            IsInsurable: isInsurable,
                            Kind: SalaryComponentKind.Earning),
                    ]),
            ],
            Insurance: new InsuranceFacts(IsInsured: isInsurable, InsurableWageOverride: null),
            Inputs: new PeriodAttendanceInputs(0, 0, 0, 0, 0, 0));
    }

    private static RulesSnapshot DefaultRules(
        decimal? otDay = null,
        decimal? otNight = null,
        decimal? otHoliday = null,
        IReadOnlyList<TaxBracket>? brackets = null)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            // [VERIFY] BRD seed figures
            [PayrollRuleKeys.InsuranceWageMin] = MoneyJson(WageMin),
            [PayrollRuleKeys.InsuranceWageMax] = MoneyJson(WageMax),
            [PayrollRuleKeys.InsuranceEmployeeRate] = PercentJson(EmployeeInsuranceRate),
            [PayrollRuleKeys.InsuranceEmployerRate] = PercentJson(EmployerInsuranceRate),
            [PayrollRuleKeys.FundMartyrsRate] = PercentJson(MartyrsRate),
            [PayrollRuleKeys.FundEmergencyEmployerRate] = PercentJson(EmergencyEmployerRate),
            [PayrollRuleKeys.TaxPersonalExemptionAnnual] = MoneyJson(PersonalExemptionAnnual),
            [PayrollRuleKeys.TaxBrackets] = BracketsJson(brackets ?? []),
            [PayrollRuleKeys.OvertimeMultiplierDay] = MultiplierJson(otDay),
            [PayrollRuleKeys.OvertimeMultiplierNight] = MultiplierJson(otNight),
            [PayrollRuleKeys.OvertimeMultiplierHoliday] = MultiplierJson(otHoliday),
            [PayrollRuleKeys.PayrollPeriodStartDay] = """{"value":1}""",
            [PayrollRuleKeys.PayrollWorkingDaysBasis] = """{"value":"fixed_30"}""",
            [PayrollRuleKeys.PayrollSecondApprovalRequired] = """{"value":true}""",
            [PayrollRuleKeys.PayrollRounding] = """{"value":"half_away_from_zero"}""",
            [PayrollRuleKeys.PayrollTaxMethod] = """{"value":"monthly_slab"}""",
        };
        return RulesSnapshot.From(map);
    }

    private static string MoneyJson(decimal amount) =>
        JsonSerializer.Serialize(new { amount });

    private static string PercentJson(decimal rate) =>
        JsonSerializer.Serialize(new { rate });

    private static string MultiplierJson(decimal? multiplier) =>
        JsonSerializer.Serialize(new { multiplier });

    private static string BracketsJson(IReadOnlyList<TaxBracket> brackets) =>
        JsonSerializer.Serialize(new
        {
            brackets = brackets.Select(b => new { from = b.From, to = b.To, rate = b.RatePercent }).ToArray(),
        });
}
