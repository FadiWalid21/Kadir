using Kadr.SharedKernel.Domain;
using Kadr.SharedKernel.Ids;

namespace Kadr.Platform.Domain.Payroll;

public sealed class EmployeeSalary : AggregateRoot, ITenantOwned, IAuditable
{
    private readonly List<EmployeeSalaryComponent> _components = [];

    private EmployeeSalary()
    {
    }

    public Guid TenantId { get; private set; }
    public Guid EmployeeId { get; private set; }
    public DateOnly EffectiveFrom { get; private set; }
    public DateOnly? EffectiveTo { get; private set; }
    public string Currency { get; private set; } = "EGP";
    public decimal BasicAmount { get; private set; }
    public string ChangeReason { get; private set; } = string.Empty;
    public IReadOnlyList<EmployeeSalaryComponent> Components => _components;
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid? CreatedBy { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public Guid? UpdatedBy { get; private set; }

    public static EmployeeSalary Create(
        Guid tenantId,
        Guid employeeId,
        DateOnly effectiveFrom,
        string currency,
        decimal basicAmount,
        string changeReason,
        IEnumerable<(Guid PayComponentId, decimal AmountOrRate)> components)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(changeReason);
        ArgumentException.ThrowIfNullOrWhiteSpace(currency);
        if (basicAmount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(basicAmount));
        }

        var currencyNorm = currency.Trim().ToUpperInvariant();
        if (currencyNorm.Length != 3)
        {
            throw new ArgumentException("Currency must be a 3-letter code.", nameof(currency));
        }

        var entity = new EmployeeSalary
        {
            Id = GuidV7.NewGuid(),
            TenantId = tenantId,
            EmployeeId = employeeId,
            EffectiveFrom = effectiveFrom,
            EffectiveTo = null,
            Currency = currencyNorm,
            BasicAmount = decimal.Round(basicAmount, 2, MidpointRounding.AwayFromZero),
            ChangeReason = changeReason.Trim(),
            CreatedAt = DateTimeOffset.UtcNow,
        };

        var seen = new HashSet<Guid>();
        foreach (var (payComponentId, amountOrRate) in components)
        {
            if (payComponentId == Guid.Empty)
            {
                throw new ArgumentException("Pay component id is required.");
            }

            if (!seen.Add(payComponentId))
            {
                throw new ArgumentException("Duplicate pay component on salary.");
            }

            if (amountOrRate < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amountOrRate));
            }

            entity._components.Add(EmployeeSalaryComponent.Create(
                tenantId,
                entity.Id,
                payComponentId,
                amountOrRate));
        }

        return entity;
    }

    public void Close(DateOnly effectiveTo)
    {
        if (EffectiveTo is not null)
        {
            throw new InvalidOperationException("Salary period is already closed.");
        }

        if (effectiveTo < EffectiveFrom)
        {
            throw new ArgumentOutOfRangeException(nameof(effectiveTo));
        }

        EffectiveTo = effectiveTo;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
