using Kadr.Platform.Domain.Attendance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kadr.Infrastructure.Persistence.Configurations;

public sealed class WorkLocationConfiguration : IEntityTypeConfiguration<WorkLocation>
{
    public void Configure(EntityTypeBuilder<WorkLocation> builder)
    {
        builder.ToTable("work_locations");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.NameAr).HasMaxLength(200).IsRequired();
        builder.Property(x => x.NameEn).HasMaxLength(200);
        builder.Property(x => x.Type).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.Address).HasMaxLength(500);
        builder.HasIndex(x => new { x.TenantId, x.IsActive });
        builder.HasIndex(x => new { x.TenantId, x.Type });
        builder.HasIndex(x => new { x.TenantId, x.LinkedBranchId });
        builder.Ignore(x => x.DomainEvents);
    }
}

public sealed class WorkLocationAssignmentConfiguration : IEntityTypeConfiguration<WorkLocationAssignment>
{
    public void Configure(EntityTypeBuilder<WorkLocationAssignment> builder)
    {
        builder.ToTable("work_location_assignments");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Scope).HasConversion<string>().HasMaxLength(32);

        builder.HasOne<WorkLocation>()
            .WithMany()
            .HasForeignKey(x => x.WorkLocationId)
            .OnDelete(DeleteBehavior.Cascade);

        // One assignment per (location, scope key).
        builder.HasIndex(x => new { x.TenantId, x.WorkLocationId, x.EmployeeId })
            .IsUnique()
            .HasFilter("scope = 'Employee'");
        builder.HasIndex(x => new { x.TenantId, x.WorkLocationId, x.DepartmentId })
            .IsUnique()
            .HasFilter("scope = 'Department'");
        builder.HasIndex(x => new { x.TenantId, x.WorkLocationId, x.BranchId })
            .IsUnique()
            .HasFilter("scope = 'Branch'");

        // Punch-time resolution lookups.
        builder.HasIndex(x => new { x.TenantId, x.EmployeeId });
        builder.HasIndex(x => new { x.TenantId, x.DepartmentId });
        builder.HasIndex(x => new { x.TenantId, x.BranchId });
    }
}

public sealed class EmployeeAttendancePolicyConfiguration : IEntityTypeConfiguration<EmployeeAttendancePolicy>
{
    public void Configure(EntityTypeBuilder<EmployeeAttendancePolicy> builder)
    {
        builder.ToTable("employee_attendance_policies");
        builder.HasKey(x => x.EmployeeId);
        builder.HasIndex(x => new { x.TenantId, x.EmployeeId });
    }
}
