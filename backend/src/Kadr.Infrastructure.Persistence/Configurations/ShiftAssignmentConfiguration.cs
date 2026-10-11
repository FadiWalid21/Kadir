using Kadr.Platform.Domain.Attendance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kadr.Infrastructure.Persistence.Configurations;

public sealed class ShiftAssignmentConfiguration : IEntityTypeConfiguration<ShiftAssignment>
{
    public void Configure(EntityTypeBuilder<ShiftAssignment> builder)
    {
        builder.ToTable("shift_assignments");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.TenantId, x.EmployeeId, x.WorkDate }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.WorkDate });
    }
}
