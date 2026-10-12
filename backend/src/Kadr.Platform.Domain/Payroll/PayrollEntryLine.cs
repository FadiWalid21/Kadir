using Kadr.SharedKernel.Domain;
using Kadr.SharedKernel.Ids;

namespace Kadr.Platform.Domain.Payroll;

public sealed class PayrollEntryLine : Entity, ITenantOwned
{
    private PayrollEntryLine()
    {
    }

    public Guid TenantId { get; private set; }
    public Guid EntryId { get; private set; }
    public string LineType { get; private set; } = string.Empty;
    public string? ComponentKey { get; private set; }
    public string LabelAr { get; private set; } = string.Empty;
    public string LabelEn { get; private set; } = string.Empty;
    public decimal Amount { get; private set; }
    public string? CalcExplanationJson { get; private set; }
    public int SortOrder { get; private set; }

    public static PayrollEntryLine Create(
        Guid tenantId,
        Guid entryId,
        string lineType,
        string? componentKey,
        string labelAr,
        string labelEn,
        decimal amount,
        string? calcExplanationJson,
        int sortOrder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(lineType);
        ArgumentException.ThrowIfNullOrWhiteSpace(labelAr);
        ArgumentException.ThrowIfNullOrWhiteSpace(labelEn);

        return new PayrollEntryLine
        {
            Id = GuidV7.NewGuid(),
            TenantId = tenantId,
            EntryId = entryId,
            LineType = lineType.Trim(),
            ComponentKey = string.IsNullOrWhiteSpace(componentKey) ? null : componentKey.Trim(),
            LabelAr = labelAr.Trim(),
            LabelEn = labelEn.Trim(),
            Amount = decimal.Round(amount, 2, MidpointRounding.AwayFromZero),
            CalcExplanationJson = calcExplanationJson,
            SortOrder = sortOrder,
        };
    }
}
