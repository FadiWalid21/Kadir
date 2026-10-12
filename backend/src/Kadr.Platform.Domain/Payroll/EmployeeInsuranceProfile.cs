using Kadr.SharedKernel.Domain;
using Kadr.SharedKernel.Ids;

namespace Kadr.Platform.Domain.Payroll;

public sealed class EmployeeInsuranceProfile : AggregateRoot, ITenantOwned, IAuditable
{
    private EmployeeInsuranceProfile()
    {
    }

    public Guid TenantId { get; private set; }
    public Guid EmployeeId { get; private set; }
    public string? InsuranceNumber { get; private set; }
    public DateOnly? InsuranceStartDate { get; private set; }
    public decimal? InsurableWageOverride { get; private set; }
    public bool IsInsured { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid? CreatedBy { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public Guid? UpdatedBy { get; private set; }

    public static EmployeeInsuranceProfile Create(
        Guid tenantId,
        Guid employeeId,
        bool isInsured,
        string? insuranceNumber,
        DateOnly? insuranceStartDate,
        decimal? insurableWageOverride)
    {
        var entity = new EmployeeInsuranceProfile
        {
            Id = GuidV7.NewGuid(),
            TenantId = tenantId,
            EmployeeId = employeeId,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        entity.Apply(isInsured, insuranceNumber, insuranceStartDate, insurableWageOverride);
        return entity;
    }

    public void Update(
        bool isInsured,
        string? insuranceNumber,
        DateOnly? insuranceStartDate,
        decimal? insurableWageOverride)
    {
        Apply(isInsured, insuranceNumber, insuranceStartDate, insurableWageOverride);
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    private void Apply(
        bool isInsured,
        string? insuranceNumber,
        DateOnly? insuranceStartDate,
        decimal? insurableWageOverride)
    {
        var number = string.IsNullOrWhiteSpace(insuranceNumber) ? null : insuranceNumber.Trim();
        if (insurableWageOverride is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(insurableWageOverride));
        }

        if (isInsured)
        {
            if (string.IsNullOrWhiteSpace(number))
            {
                throw new ArgumentException("Insurance number is required when insured.", nameof(insuranceNumber));
            }

            if (insuranceStartDate is null)
            {
                throw new ArgumentException("Insurance start date is required when insured.", nameof(insuranceStartDate));
            }
        }

        IsInsured = isInsured;
        InsuranceNumber = number;
        InsuranceStartDate = insuranceStartDate;
        InsurableWageOverride = insurableWageOverride is null
            ? null
            : decimal.Round(insurableWageOverride.Value, 2, MidpointRounding.AwayFromZero);
    }
}
