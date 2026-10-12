using Kadr.Platform.Domain.Payroll;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kadr.Infrastructure.Persistence.Configurations;

public sealed class EmployeeInsuranceProfileConfiguration : IEntityTypeConfiguration<EmployeeInsuranceProfile>
{
    public void Configure(EntityTypeBuilder<EmployeeInsuranceProfile> builder)
    {
        builder.ToTable("employee_insurance_profiles");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.InsuranceNumber).HasMaxLength(64);
        builder.Property(x => x.InsurableWageOverride).HasPrecision(18, 2);
        builder.HasIndex(x => new { x.TenantId, x.EmployeeId }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.IsInsured });
        builder.Ignore(x => x.DomainEvents);
    }
}
