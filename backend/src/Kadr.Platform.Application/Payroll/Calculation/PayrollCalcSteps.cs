using Kadr.SharedKernel.Constants;

namespace Kadr.Platform.Application.Payroll.Calculation;

/// <summary>1. Mid-period salary proration by day — one earning line per segment.</summary>
public sealed class SalaryProrationStep : IPayrollCalcStep
{
    public string Name => "salary_proration";

    public void Execute(PayrollCalcContext context)
    {
        var input = context.Input;
        context.DaysBasis = ResolveDaysBasis(context);
        var periodStart = input.PeriodStart;
        var periodEnd = input.PeriodEnd;

        decimal proratedBasic = 0m;

        foreach (var segment in input.SalarySegments.OrderBy(s => s.EffectiveFrom))
        {
            var segStart = segment.EffectiveFrom < periodStart ? periodStart : segment.EffectiveFrom;
            var rawEnd = segment.EffectiveTo is null
                ? periodEnd
                : segment.EffectiveTo.Value;
            var segEnd = rawEnd > periodEnd ? periodEnd : rawEnd;

            if (segment.EffectiveTo is not null && segment.EffectiveTo < periodStart)
            {
                continue;
            }

            if (segment.EffectiveFrom > periodEnd || segStart > segEnd)
            {
                continue;
            }

            var days = segEnd.DayNumber - segStart.DayNumber + 1;
            if (days <= 0)
            {
                continue;
            }

            var factor = (decimal)days / context.DaysBasis;
            var basicAmount = segment.BasicAmount * factor;
            proratedBasic += basicAmount;

            var explanation =
                $"days={days}/{context.DaysBasis}; monthly_basic={segment.BasicAmount}; factor={factor:0.####}";
            context.AddLine(
                PayrollCalcLineTypes.Earning,
                PayrollCalcComponentKeys.BasicProrated,
                "راتب أساسي (نسبي)",
                "Basic salary (prorated)",
                basicAmount,
                explanation);

            var basicRounded = PayrollMoney.Round(basicAmount, context.Rounding);
            if (segment.BasicIsTaxable)
            {
                context.TaxableEarnings += basicRounded;
            }

            if (segment.BasicIsInsurable)
            {
                context.InsurableEarnings += basicRounded;
            }

            foreach (var component in segment.Components.Where(c => c.Kind == SalaryComponentKind.Earning))
            {
                var amount = component.Amount * factor;
                context.AddLine(
                    PayrollCalcLineTypes.Earning,
                    component.ComponentKey,
                    component.LabelAr,
                    component.LabelEn,
                    amount,
                    $"days={days}/{context.DaysBasis}; monthly={component.Amount}");

                var rounded = PayrollMoney.Round(amount, context.Rounding);
                if (component.IsTaxable)
                {
                    context.TaxableEarnings += rounded;
                }

                if (component.IsInsurable)
                {
                    context.InsurableEarnings += rounded;
                }
            }
        }

        context.ProratedBasicTotal = PayrollMoney.Round(proratedBasic, context.Rounding);
        var periodEndSegment = input.SalarySegments
            .Where(s => s.EffectiveFrom <= periodEnd && (s.EffectiveTo is null || s.EffectiveTo >= periodEnd))
            .OrderByDescending(s => s.EffectiveFrom)
            .FirstOrDefault()
            ?? input.SalarySegments.OrderByDescending(s => s.EffectiveFrom).FirstOrDefault();
        context.PeriodEndMonthlyBasic = periodEndSegment?.BasicAmount ?? 0m;
    }

    internal static int ResolveDaysBasis(PayrollCalcContext context)
    {
        var basis = context.Rules.GetWorkingDaysBasis();
        var calendarDays = context.Input.PeriodEnd.DayNumber - context.Input.PeriodStart.DayNumber + 1;
        return basis switch
        {
            "fixed_30" => 30,
            "calendar" => calendarDays,
            "working_days" => calendarDays, // day-level calendar not available in pure engine; use span
            _ => 30,
        };
    }
}

/// <summary>2. Resolve period inputs already supplied on <see cref="PayrollCalcInput"/>.</summary>
public sealed class InputsStep : IPayrollCalcStep
{
    public string Name => "inputs";

    public void Execute(PayrollCalcContext context)
    {
        // Attendance/leave facts arrive pre-collected. Ensure denominator is set if proration skipped empty segments.
        if (context.DaysBasis <= 0)
        {
            context.DaysBasis = SalaryProrationStep.ResolveDaysBasis(context);
        }
    }
}

/// <summary>3. Overtime earnings + gross = sum of earning lines.</summary>
public sealed class EarningsStep : IPayrollCalcStep
{
    public const int StandardHoursPerDay = 8;

    public string Name => "earnings";

    public void Execute(PayrollCalcContext context)
    {
        var inputs = context.Input.Inputs;
        var needsOt = inputs.OvertimeHoursDay > 0
            || inputs.OvertimeHoursNight > 0
            || inputs.OvertimeHoursHoliday > 0;

        if (needsOt)
        {
            var dayMult = context.Rules.GetOptionalMultiplier(PayrollRuleKeys.OvertimeMultiplierDay);
            var nightMult = context.Rules.GetOptionalMultiplier(PayrollRuleKeys.OvertimeMultiplierNight);
            var holidayMult = context.Rules.GetOptionalMultiplier(PayrollRuleKeys.OvertimeMultiplierHoliday);

            var anyMissing =
                (inputs.OvertimeHoursDay > 0 && dayMult is null)
                || (inputs.OvertimeHoursNight > 0 && nightMult is null)
                || (inputs.OvertimeHoursHoliday > 0 && holidayMult is null);

            if (anyMissing || (dayMult is null && nightMult is null && holidayMult is null && needsOt))
            {
                context.AddWarning(
                    PayrollCalcWarningCodes.OvertimeMissingMultipliers,
                    "مضاعفات الوقت الإضافي غير محددة؛ مبلغ الوقت الإضافي صفر.",
                    "Overtime multipliers are missing; overtime amount is zero.");
            }

            var hourly = context.DaysBasis > 0
                ? context.PeriodEndMonthlyBasic / context.DaysBasis / StandardHoursPerDay
                : 0m;

            AddOt(context, PayrollCalcComponentKeys.OvertimeDay, "وقت إضافي نهاري", "Day overtime",
                inputs.OvertimeHoursDay, hourly, dayMult);
            AddOt(context, PayrollCalcComponentKeys.OvertimeNight, "وقت إضافي ليلي", "Night overtime",
                inputs.OvertimeHoursNight, hourly, nightMult);
            AddOt(context, PayrollCalcComponentKeys.OvertimeHoliday, "وقت إضافي في عطلة", "Holiday overtime",
                inputs.OvertimeHoursHoliday, hourly, holidayMult);
        }

        context.Gross = context.SumLines(PayrollCalcLineTypes.Earning);
    }

    private static void AddOt(
        PayrollCalcContext context,
        string key,
        string labelAr,
        string labelEn,
        decimal hours,
        decimal hourlyRate,
        decimal? multiplier)
    {
        if (hours <= 0)
        {
            return;
        }

        var amount = multiplier is null ? 0m : hours * hourlyRate * multiplier.Value;
        if (multiplier is not null)
        {
            context.AddLine(
                PayrollCalcLineTypes.Earning,
                key,
                labelAr,
                labelEn,
                amount,
                $"hours={hours}; hourly={hourlyRate}; multiplier={multiplier}");
            var rounded = PayrollMoney.Round(amount, context.Rounding);
            context.TaxableEarnings += rounded;
            // OT typically not insurable unless a component says so; leave InsurableEarnings unchanged.
        }
        else
        {
            context.AddLine(
                PayrollCalcLineTypes.Earning,
                key,
                labelAr,
                labelEn,
                0m,
                "multiplier missing; amount=0");
        }
    }
}

/// <summary>4. Absence / unpaid leave / late as explicit deduction lines.</summary>
public sealed class UnpaidDeductionsStep : IPayrollCalcStep
{
    public string Name => "unpaid_deductions";

    public void Execute(PayrollCalcContext context)
    {
        var inputs = context.Input.Inputs;
        var daily = context.DaysBasis > 0
            ? context.PeriodEndMonthlyBasic / context.DaysBasis
            : 0m;
        var minuteRate = daily / EarningsStep.StandardHoursPerDay / 60m;

        if (inputs.UnpaidLeaveDays > 0)
        {
            var amount = daily * inputs.UnpaidLeaveDays;
            context.AddLine(
                PayrollCalcLineTypes.Deduction,
                PayrollCalcComponentKeys.UnpaidLeave,
                "خصم إجازة بدون مرتب",
                "Unpaid leave deduction",
                amount,
                $"days={inputs.UnpaidLeaveDays}; daily={daily}");
        }

        if (inputs.AbsenceDays > 0)
        {
            var amount = daily * inputs.AbsenceDays;
            context.AddLine(
                PayrollCalcLineTypes.Deduction,
                PayrollCalcComponentKeys.Absence,
                "خصم غياب",
                "Absence deduction",
                amount,
                $"days={inputs.AbsenceDays}; daily={daily}");
        }

        if (inputs.LateMinutes > 0)
        {
            var amount = minuteRate * inputs.LateMinutes;
            context.AddLine(
                PayrollCalcLineTypes.Deduction,
                PayrollCalcComponentKeys.Late,
                "خصم تأخير",
                "Late deduction",
                amount,
                $"minutes={inputs.LateMinutes}; minute_rate={minuteRate}");
        }
    }
}

/// <summary>5. Insurable wage = sum of insurable earnings, clamped to [min, max] (or override).</summary>
public sealed class InsurableWageStep : IPayrollCalcStep
{
    public string Name => "insurable_wage";

    public void Execute(PayrollCalcContext context)
    {
        if (!context.Input.Insurance.IsInsured)
        {
            context.InsurableWage = 0m;
            return;
        }

        var raw = context.Input.Insurance.InsurableWageOverride ?? context.InsurableEarnings;
        // [VERIFY] wage_min / wage_max from rules snapshot (BRD seed 2700 / 16700)
        var min = context.Rules.GetMoney(PayrollRuleKeys.InsuranceWageMin);
        var max = context.Rules.GetMoney(PayrollRuleKeys.InsuranceWageMax);
        var clamped = raw;
        if (clamped < min)
        {
            clamped = min;
        }

        if (clamped > max)
        {
            clamped = max;
        }

        context.InsurableWage = PayrollMoney.Round(clamped, context.Rounding);
    }
}

/// <summary>6. Employee + employer insurance and emergency employer fund.</summary>
public sealed class InsuranceContributionsStep : IPayrollCalcStep
{
    public string Name => "insurance_contributions";

    public void Execute(PayrollCalcContext context)
    {
        if (!context.Input.Insurance.IsInsured)
        {
            return;
        }

        // [VERIFY] rates from rules (BRD seed employee 11%, employer 18.75%, emergency 1%)
        var employeeRate = context.Rules.GetPercent(PayrollRuleKeys.InsuranceEmployeeRate);
        var employerRate = context.Rules.GetPercent(PayrollRuleKeys.InsuranceEmployerRate);
        var emergencyRate = context.Rules.GetPercent(PayrollRuleKeys.FundEmergencyEmployerRate);
        var wage = context.InsurableWage;

        var employeeAmount = wage * employeeRate / 100m;
        var employerAmount = wage * employerRate / 100m;
        var emergencyAmount = wage * emergencyRate / 100m;

        context.AddLine(
            PayrollCalcLineTypes.Deduction,
            PayrollCalcComponentKeys.EmployeeInsurance,
            "تأمينات اجتماعية (موظف)",
            "Social insurance (employee)",
            employeeAmount,
            $"insurable={wage}; rate={employeeRate}%");

        context.AddLine(
            PayrollCalcLineTypes.EmployerCost,
            PayrollCalcComponentKeys.EmployerInsurance,
            "تأمينات اجتماعية (صاحب العمل)",
            "Social insurance (employer)",
            employerAmount,
            $"insurable={wage}; rate={employerRate}%");

        context.AddLine(
            PayrollCalcLineTypes.EmployerCost,
            PayrollCalcComponentKeys.EmergencyFundEmployer,
            "صندوق الطوارئ (صاحب العمل)",
            "Emergency fund (employer)",
            emergencyAmount,
            $"insurable={wage}; rate={emergencyRate}%");

        context.EmployeeInsurance = PayrollMoney.Round(employeeAmount, context.Rounding);
        context.EmployerInsurance = PayrollMoney.Round(employerAmount, context.Rounding);
        context.EmergencyEmployerCost = PayrollMoney.Round(emergencyAmount, context.Rounding);
    }
}

/// <summary>7. Martyrs fund on gross (employee deduction).</summary>
public sealed class MartyrsFundStep : IPayrollCalcStep
{
    public string Name => "martyrs_fund";

    public void Execute(PayrollCalcContext context)
    {
        // [VERIFY] fund.martyrs_rate from rules (BRD seed 0.05%)
        var rate = context.Rules.GetPercent(PayrollRuleKeys.FundMartyrsRate);
        var amount = context.Gross * rate / 100m;
        if (amount == 0m && rate == 0m)
        {
            return;
        }

        context.AddLine(
            PayrollCalcLineTypes.Deduction,
            PayrollCalcComponentKeys.MartyrsFund,
            "صندوق الشهداء",
            "Martyrs fund",
            amount,
            $"gross={context.Gross}; rate={rate}%");
    }
}

/// <summary>8. Income tax via monthly_slab (annual brackets and exemption ÷ 12).</summary>
public sealed class IncomeTaxStep : IPayrollCalcStep
{
    public string Name => "income_tax";

    public void Execute(PayrollCalcContext context)
    {
        var brackets = context.Rules.GetTaxBrackets();
        if (brackets.Count == 0)
        {
            context.IncomeTax = 0m;
            context.TaxableIncome = ComputeTaxableIncome(context);
            context.AddWarning(
                PayrollCalcWarningCodes.TaxEmptyBrackets,
                "شرائح الضريبة فارغة؛ ضريبة الدخل صفر ويجب التحقق قبل الاعتماد.",
                "Tax brackets are empty; income tax is zero and must be verified before approval.");
            context.AddLine(
                PayrollCalcLineTypes.Deduction,
                PayrollCalcComponentKeys.IncomeTax,
                "ضريبة كسب العمل",
                "Income tax",
                0m,
                "empty brackets; tax=0");
            return;
        }

        var taxable = ComputeTaxableIncome(context);
        context.TaxableIncome = taxable;

        var method = context.Rules.GetTaxMethod();
        decimal tax;
        if (method == "monthly_slab")
        {
            tax = ComputeMonthlySlabTax(taxable, brackets);
        }
        else
        {
            // Unsupported methods fall back to monthly_slab until accountant confirms [VERIFY]
            tax = ComputeMonthlySlabTax(taxable, brackets);
        }

        context.IncomeTax = PayrollMoney.Round(tax, context.Rounding);
        context.AddLine(
            PayrollCalcLineTypes.Deduction,
            PayrollCalcComponentKeys.IncomeTax,
            "ضريبة كسب العمل",
            "Income tax",
            tax,
            $"method={method}; taxable={taxable}");
    }

    private static decimal ComputeTaxableIncome(PayrollCalcContext context)
    {
        // [VERIFY] personal exemption annual from rules (BRD seed 20000) ÷ 12 for monthly_slab
        var exemptionAnnual = context.Rules.GetMoney(PayrollRuleKeys.TaxPersonalExemptionAnnual);
        var monthlyExemption = exemptionAnnual / 12m;
        var taxable = context.TaxableEarnings - context.EmployeeInsurance - monthlyExemption;
        return taxable < 0m ? 0m : PayrollMoney.Round(taxable, context.Rounding);
    }

    /// <summary>
    /// Progressive tax on monthly taxable income using annual brackets ÷ 12.
    /// </summary>
    private static decimal ComputeMonthlySlabTax(decimal monthlyTaxable, IReadOnlyList<TaxBracket> annualBrackets)
    {
        if (monthlyTaxable <= 0m)
        {
            return 0m;
        }

        decimal tax = 0m;
        foreach (var band in annualBrackets)
        {
            var fromMonthly = band.From / 12m;
            var toMonthly = band.To is null ? (decimal?)null : band.To.Value / 12m;
            if (monthlyTaxable <= fromMonthly)
            {
                break;
            }

            var upper = toMonthly is null ? monthlyTaxable : Math.Min(monthlyTaxable, toMonthly.Value);
            var slice = upper - fromMonthly;
            if (slice > 0m)
            {
                tax += slice * band.RatePercent / 100m;
            }
        }

        return tax;
    }
}

/// <summary>
/// 9. Loans / advances / penalties — documented no-op stub until Feature 23.
/// </summary>
public sealed class OtherDeductionsStubStep : IPayrollCalcStep
{
    public string Name => "other_deductions_stub";

    public void Execute(PayrollCalcContext context)
    {
        // F23: loan installments, advances, bonuses, penalties, HR adjustments.
        context.OtherDeductions = 0m;
    }
}

/// <summary>10. Net = gross − employee deductions; never negative; flag excess.</summary>
public sealed class NetPayStep : IPayrollCalcStep
{
    public string Name => "net_pay";

    public void Execute(PayrollCalcContext context)
    {
        var deductions = context.SumLines(PayrollCalcLineTypes.Deduction);
        var net = context.Gross - deductions;
        if (net < 0m)
        {
            context.ExcessDeductions = PayrollMoney.Round(-net, context.Rounding);
            context.DeductionsExceedGross = true;
            context.Net = 0m;
            context.AddWarning(
                PayrollCalcWarningCodes.NetDeductionsExceedGross,
                "الخصومات تجاوزت الإجمالي؛ صافي الراتب صفر والمبلغ الزائد للمراجعة.",
                "Deductions exceed gross; net is zero and the excess is flagged for review.");
        }
        else
        {
            context.Net = PayrollMoney.Round(net, context.Rounding);
            context.ExcessDeductions = 0m;
            context.DeductionsExceedGross = false;
        }

        context.EmployerCostTotal = context.SumLines(PayrollCalcLineTypes.EmployerCost);
    }
}

/// <summary>11. Totals are sums of already-rounded lines (policy from payroll.rounding).</summary>
public sealed class RoundingTotalsStep : IPayrollCalcStep
{
    public string Name => "rounding_totals";

    public void Execute(PayrollCalcContext context)
    {
        context.Gross = context.SumLines(PayrollCalcLineTypes.Earning);
        context.EmployeeInsurance = context.Lines
            .Where(l => l.ComponentKey == PayrollCalcComponentKeys.EmployeeInsurance)
            .Sum(l => l.Amount);
        context.EmployerInsurance = context.Lines
            .Where(l => l.ComponentKey == PayrollCalcComponentKeys.EmployerInsurance)
            .Sum(l => l.Amount);
        context.IncomeTax = context.Lines
            .Where(l => l.ComponentKey == PayrollCalcComponentKeys.IncomeTax)
            .Sum(l => l.Amount);
        context.OtherDeductions = 0m;
        context.EmployerCostTotal = context.SumLines(PayrollCalcLineTypes.EmployerCost);

        if (!context.DeductionsExceedGross)
        {
            var deductions = context.SumLines(PayrollCalcLineTypes.Deduction);
            context.Net = PayrollMoney.Round(context.Gross - deductions, context.Rounding);
        }
    }
}
