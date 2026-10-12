using Kadr.Platform.Domain.Attendance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kadr.Infrastructure.Persistence.Configurations;

public sealed class AttendanceCorrectionRequestConfiguration
    : IEntityTypeConfiguration<AttendanceCorrectionRequest>
{
    public void Configure(EntityTypeBuilder<AttendanceCorrectionRequest> builder)
    {
        builder.ToTable("attendance_correction_requests");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ProblemType).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Reason).HasMaxLength(2000).IsRequired();
        builder.Property(x => x.RejectReason).HasMaxLength(2000);
        builder.HasIndex(x => new { x.TenantId, x.EmployeeId, x.CreatedAt });
        builder.HasIndex(x => new { x.TenantId, x.Status, x.CreatedAt });
        builder.HasIndex(x => new { x.TenantId, x.WorkDate });
    }
}
