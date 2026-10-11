using Kadr.Platform.Domain.Attendance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kadr.Infrastructure.Persistence.Configurations;

public sealed class AttendancePunchConfiguration : IEntityTypeConfiguration<AttendancePunch>
{
    public void Configure(EntityTypeBuilder<AttendancePunch> builder)
    {
        builder.ToTable("attendance_punches");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.OutsideReason).HasMaxLength(500);
        builder.Property(x => x.Kind).HasConversion<string>().HasMaxLength(16);
        builder.Property(x => x.Source).HasConversion<string>().HasMaxLength(16);
        builder.HasIndex(x => new { x.TenantId, x.EmployeeId, x.ClientRequestId }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.EmployeeId, x.WorkDate });
        builder.HasIndex(x => new { x.TenantId, x.WorkDate });
        builder.Ignore(x => x.DomainEvents);
    }
}
