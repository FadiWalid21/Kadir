using System.Text.Json;
using Kadr.Platform.Domain.Payroll;
using Kadr.SharedKernel.Constants;

namespace Kadr.Infrastructure.Persistence.Payroll;

/// <summary>
/// Official-default catalog for Egypt. All figures are [VERIFY] — not for real payroll until confirmed.
/// </summary>
public static class EgyptStatutoryRuleCatalog
{
    public const string Country = "EG";
    private static readonly DateOnly DefaultEffectiveFrom = new(2024, 1, 1);

    public static IReadOnlyList<StatutoryRuleSet> CreateDefaults()
    {
        // [VERIFY] BRD section 3.1 seed figures — accountant must confirm before real payroll.
        return
        [
            Money(PayrollRuleKeys.InsuranceWageMin, 2700m, "BRD insurance wage min [VERIFY]"),
            Money(PayrollRuleKeys.InsuranceWageMax, 16700m, "BRD insurance wage max [VERIFY]"),
            Percent(PayrollRuleKeys.InsuranceEmployeeRate, 11m, "BRD employee insurance rate [VERIFY]"),
            Percent(PayrollRuleKeys.InsuranceEmployerRate, 18.75m, "BRD employer insurance rate [VERIFY]"),
            Percent(PayrollRuleKeys.FundMartyrsRate, 0.05m, "BRD martyrs fund [VERIFY]"),
            Percent(PayrollRuleKeys.FundEmergencyEmployerRate, 1m, "BRD emergency fund employer [VERIFY]"),
            Money(PayrollRuleKeys.TaxPersonalExemptionAnnual, 20000m, "BRD personal exemption [VERIFY]"),
            Table(PayrollRuleKeys.TaxBrackets, "Empty until accountant supplies brackets [VERIFY]"),
            DecimalNullable(PayrollRuleKeys.OvertimeMultiplierDay, "Empty until accountant supplies OT day multiplier [VERIFY]"),
            DecimalNullable(PayrollRuleKeys.OvertimeMultiplierNight, "Empty until accountant supplies OT night multiplier [VERIFY]"),
            DecimalNullable(PayrollRuleKeys.OvertimeMultiplierHoliday, "Empty until accountant supplies OT holiday multiplier [VERIFY]"),
            Int(PayrollRuleKeys.PayrollPeriodStartDay, 1, "Default calendar month start"),
            Enum(PayrollRuleKeys.PayrollWorkingDaysBasis, "fixed_30", "Proration basis"),
            Bool(PayrollRuleKeys.PayrollSecondApprovalRequired, true, "Second approver recommended"),
            Enum(PayrollRuleKeys.PayrollRounding, "half_away_from_zero", "Round per line to 2 decimals"),
            Enum(PayrollRuleKeys.PayrollTaxMethod, "monthly_slab", "Annual brackets / exemption ÷ 12 [VERIFY]"),
        ];
    }

    private static StatutoryRuleSet Money(string key, decimal amount, string note) =>
        StatutoryRuleSet.Create(
            Country,
            key,
            PayrollRuleValueTypes.Money,
            JsonSerializer.Serialize(new { amount }),
            DefaultEffectiveFrom,
            version: 1,
            note,
            isVerified: false);

    private static StatutoryRuleSet Percent(string key, decimal rate, string note) =>
        StatutoryRuleSet.Create(
            Country,
            key,
            PayrollRuleValueTypes.Percent,
            JsonSerializer.Serialize(new { rate }),
            DefaultEffectiveFrom,
            version: 1,
            note,
            isVerified: false);

    private static StatutoryRuleSet Table(string key, string note) =>
        StatutoryRuleSet.Create(
            Country,
            key,
            PayrollRuleValueTypes.Table,
            JsonSerializer.Serialize(new { brackets = Array.Empty<object>() }),
            DefaultEffectiveFrom,
            version: 1,
            note,
            isVerified: false);

    private static StatutoryRuleSet DecimalNullable(string key, string note) =>
        StatutoryRuleSet.Create(
            Country,
            key,
            PayrollRuleValueTypes.Decimal,
            JsonSerializer.Serialize(new { multiplier = (decimal?)null }),
            DefaultEffectiveFrom,
            version: 1,
            note,
            isVerified: false);

    private static StatutoryRuleSet Int(string key, int value, string note) =>
        StatutoryRuleSet.Create(
            Country,
            key,
            PayrollRuleValueTypes.Int,
            JsonSerializer.Serialize(new { value }),
            DefaultEffectiveFrom,
            version: 1,
            note,
            isVerified: false);

    private static StatutoryRuleSet Bool(string key, bool value, string note) =>
        StatutoryRuleSet.Create(
            Country,
            key,
            PayrollRuleValueTypes.Bool,
            JsonSerializer.Serialize(new { value }),
            DefaultEffectiveFrom,
            version: 1,
            note,
            isVerified: false);

    private static StatutoryRuleSet Enum(string key, string value, string note) =>
        StatutoryRuleSet.Create(
            Country,
            key,
            PayrollRuleValueTypes.Enum,
            JsonSerializer.Serialize(new { value }),
            DefaultEffectiveFrom,
            version: 1,
            note,
            isVerified: false);
}
