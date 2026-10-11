using Kadr.Platform.Domain.Employees;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kadr.Infrastructure.Persistence.Configurations;

public sealed class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> builder)
    {
        builder.ToTable("employees");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.EmployeeNumber).HasMaxLength(32).IsRequired();
        builder.Property(x => x.FullNameAr).HasMaxLength(200).IsRequired();
        builder.Property(x => x.FullNameEn).HasMaxLength(200);
        builder.Property(x => x.NationalId).HasMaxLength(32);
        builder.Property(x => x.Email).HasMaxLength(320);
        builder.Property(x => x.Phone).HasMaxLength(32);
        builder.Property(x => x.Address).HasMaxLength(1000);
        builder.Property(x => x.SearchName).HasMaxLength(800).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.Gender).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.ContractType).HasConversion<string>().HasMaxLength(32);

        builder.HasIndex(x => new { x.TenantId, x.EmployeeNumber }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.NationalId })
            .IsUnique()
            .HasFilter("national_id IS NOT NULL");
        builder.HasIndex(x => new { x.TenantId, x.Status });
        builder.HasIndex(x => new { x.TenantId, x.DepartmentId });
        builder.HasIndex(x => new { x.TenantId, x.BranchId });
        builder.HasIndex(x => new { x.TenantId, x.ManagerEmployeeId });
        builder.HasIndex(x => new { x.TenantId, x.SearchName });

        builder.HasMany(x => x.EmergencyContacts)
            .WithOne()
            .HasForeignKey(x => x.EmployeeId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.EmergencyContacts).HasField("_emergencyContacts");

        builder.Ignore(x => x.DomainEvents);
    }
}

public sealed class EmergencyContactConfiguration : IEntityTypeConfiguration<EmergencyContact>
{
    public void Configure(EntityTypeBuilder<EmergencyContact> builder)
    {
        builder.ToTable("employee_emergency_contacts");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Relationship).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Phone).HasMaxLength(32).IsRequired();
        builder.HasIndex(x => new { x.TenantId, x.EmployeeId });
    }
}

public sealed class EmployeeDocumentConfiguration : IEntityTypeConfiguration<EmployeeDocument>
{
    public void Configure(EntityTypeBuilder<EmployeeDocument> builder)
    {
        builder.ToTable("employee_documents");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ReferenceNumber).HasMaxLength(100);
        builder.HasIndex(x => new { x.TenantId, x.ExpiresOn });
        builder.HasIndex(x => new { x.TenantId, x.EmployeeId });
        builder.Ignore(x => x.DomainEvents);
    }
}
