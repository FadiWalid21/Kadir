using Kadr.Platform.Domain.Attendance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kadr.Infrastructure.Persistence.Configurations;

public sealed class AttendanceDaySummaryConfiguration : IEntityTypeConfiguration<AttendanceDaySummary>
{
    public void Configure(EntityTypeBuilder<AttendanceDaySummary> builder)
    {
        builder.ToTable("attendance_day_summaries");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
        builder.HasIndex(x => new { x.TenantId, x.EmployeeId, x.WorkDate }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.WorkDate });
        builder.Ignore(x => x.DomainEvents);
    }
}
