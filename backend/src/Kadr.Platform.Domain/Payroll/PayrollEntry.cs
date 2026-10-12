using Kadr.SharedKernel.Domain;
using Kadr.SharedKernel.Ids;

namespace Kadr.Platform.Domain.Payroll;

public sealed class PayrollEntry : AggregateRoot, ITenantOwned
{
    private readonly List<PayrollEntryLine> _lines = [];

    private PayrollEntry()
    {
    }

    public Guid TenantId { get; private set; }
    public Guid RunId { get; private set; }
    public Guid EmployeeId { get; private set; }
    public string SalarySnapshotJson { get; private set; } = "{}";
    public string InputsJson { get; private set; } = "{}";
    public decimal Gross { get; private set; }
    public decimal InsurableWage { get; private set; }
    public decimal EmployeeInsurance { get; private set; }
    public decimal EmployerInsurance { get; private set; }
    public decimal TaxableIncome { get; private set; }
    public decimal IncomeTax { get; private set; }
    public decimal OtherDeductions { get; private set; }
    public decimal Net { get; private set; }
    public decimal EmployerCostTotal { get; private set; }
    public bool IsHeld { get; private set; }
    public string? HoldReason { get; private set; }
    public int WarningCount { get; private set; }
    public string? WarningsJson { get; private set; }
    public IReadOnlyList<PayrollEntryLine> Lines => _lines;

    public static PayrollEntry Create(
        Guid tenantId,
        Guid runId,
        Guid employeeId,
        string salarySnapshotJson,
        string inputsJson,
        decimal gross,
        decimal insurableWage,
        decimal employeeInsurance,
        decimal employerInsurance,
        decimal taxableIncome,
        decimal incomeTax,
        decimal otherDeductions,
        decimal net,
        decimal employerCostTotal,
        int warningCount,
        string? warningsJson,
        IReadOnlyList<PayrollEntryLineDraft> lineDrafts)
    {
        var entry = new PayrollEntry
        {
            Id = GuidV7.NewGuid(),
            TenantId = tenantId,
            RunId = runId,
            EmployeeId = employeeId,
            SalarySnapshotJson = salarySnapshotJson,
            InputsJson = inputsJson,
            Gross = Round(gross),
            InsurableWage = Round(insurableWage),
            EmployeeInsurance = Round(employeeInsurance),
            EmployerInsurance = Round(employerInsurance),
            TaxableIncome = Round(taxableIncome),
            IncomeTax = Round(incomeTax),
            OtherDeductions = Round(otherDeductions),
            Net = Round(net),
            EmployerCostTotal = Round(employerCostTotal),
            WarningCount = Math.Max(0, warningCount),
            WarningsJson = warningsJson,
        };

        for (var i = 0; i < lineDrafts.Count; i++)
        {
            var d = lineDrafts[i];
            entry._lines.Add(PayrollEntryLine.Create(
                tenantId,
                entry.Id,
                d.LineType,
                d.ComponentKey,
                d.LabelAr,
                d.LabelEn,
                d.Amount,
                d.CalcExplanationJson,
                i));
        }

        return entry;
    }

    public void Hold(string? reason)
    {
        IsHeld = true;
        HoldReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
    }

    private static decimal Round(decimal value) =>
        decimal.Round(value, 2, MidpointRounding.AwayFromZero);
}

public sealed record PayrollEntryLineDraft(
    string LineType,
    string? ComponentKey,
    string LabelAr,
    string LabelEn,
    decimal Amount,
    string? CalcExplanationJson);
