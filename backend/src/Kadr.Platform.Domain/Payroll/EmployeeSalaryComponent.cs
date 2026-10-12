using Kadr.SharedKernel.Domain;
using Kadr.SharedKernel.Ids;

namespace Kadr.Platform.Domain.Payroll;

public sealed class EmployeeSalaryComponent : Entity, ITenantOwned
{
    private EmployeeSalaryComponent()
    {
    }

    public Guid TenantId { get; private set; }
    public Guid EmployeeSalaryId { get; private set; }
    public Guid PayComponentId { get; private set; }
    public decimal AmountOrRate { get; private set; }

    public static EmployeeSalaryComponent Create(
        Guid tenantId,
        Guid employeeSalaryId,
        Guid payComponentId,
        decimal amountOrRate)
    {
        if (amountOrRate < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amountOrRate));
        }

        return new EmployeeSalaryComponent
        {
            Id = GuidV7.NewGuid(),
            TenantId = tenantId,
            EmployeeSalaryId = employeeSalaryId,
            PayComponentId = payComponentId,
            AmountOrRate = decimal.Round(amountOrRate, 2, MidpointRounding.AwayFromZero),
        };
    }
}
