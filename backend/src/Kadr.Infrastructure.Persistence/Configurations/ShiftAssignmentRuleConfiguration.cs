using Kadr.Platform.Domain.Attendance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kadr.Infrastructure.Persistence.Configurations;

public sealed class ShiftAssignmentRuleConfiguration : IEntityTypeConfiguration<ShiftAssignmentRule>
{
    public void Configure(EntityTypeBuilder<ShiftAssignmentRule> builder)
    {
        builder.ToTable("shift_assignment_rules");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TargetType).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.IsDayOff).IsRequired();
        builder.HasIndex(x => new { x.TenantId, x.TargetType, x.TargetId, x.EffectiveFrom })
            .HasDatabaseName("ix_shift_assignment_rules_tenant_target_from");
        builder.HasIndex(x => new { x.TenantId, x.EffectiveFrom, x.EffectiveTo })
            .HasDatabaseName("ix_shift_assignment_rules_tenant_effective");
    }
}
