using Kadr.Platform.Domain.Attendance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kadr.Infrastructure.Persistence.Configurations;

public sealed class AttendancePunchAttemptConfiguration : IEntityTypeConfiguration<AttendancePunchAttempt>
{
    public void Configure(EntityTypeBuilder<AttendancePunchAttempt> builder)
    {
        builder.ToTable("attendance_punch_attempts");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ReasonCode).HasMaxLength(100).IsRequired();
        builder.HasIndex(x => new { x.TenantId, x.EmployeeId, x.CreatedAt });
        builder.HasIndex(x => new { x.TenantId, x.CreatedAt });
    }
}
