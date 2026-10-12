using Kadr.Platform.Domain.Payroll;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kadr.Infrastructure.Persistence.Configurations;

public sealed class EmployeeSalaryConfiguration : IEntityTypeConfiguration<EmployeeSalary>
{
    public void Configure(EntityTypeBuilder<EmployeeSalary> builder)
    {
        builder.ToTable("employee_salaries");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        builder.Property(x => x.BasicAmount).HasPrecision(18, 2);
        builder.Property(x => x.ChangeReason).HasMaxLength(500).IsRequired();

        builder.HasIndex(x => new { x.TenantId, x.EmployeeId, x.EffectiveFrom });
        builder.HasIndex(x => new { x.TenantId, x.EmployeeId, x.EffectiveTo });
        // At most one open salary period per employee.
        builder.HasIndex(x => new { x.TenantId, x.EmployeeId })
            .IsUnique()
            .HasFilter("effective_to IS NULL");

        builder.HasMany(x => x.Components)
            .WithOne()
            .HasForeignKey(x => x.EmployeeSalaryId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.Components).HasField("_components");

        builder.Ignore(x => x.DomainEvents);
    }
}

public sealed class EmployeeSalaryComponentConfiguration : IEntityTypeConfiguration<EmployeeSalaryComponent>
{
    public void Configure(EntityTypeBuilder<EmployeeSalaryComponent> builder)
    {
        builder.ToTable("employee_salary_components");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.AmountOrRate).HasPrecision(18, 2);
        builder.HasIndex(x => new { x.TenantId, x.EmployeeSalaryId, x.PayComponentId }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.PayComponentId });
    }
}
