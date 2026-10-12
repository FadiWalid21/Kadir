using Kadr.Platform.Domain.Payroll;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kadr.Infrastructure.Persistence.Configurations;

public sealed class TenantRuleValueConfiguration : IEntityTypeConfiguration<TenantRuleValue>
{
    public void Configure(EntityTypeBuilder<TenantRuleValue> builder)
    {
        builder.ToTable("tenant_rule_values");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.RuleKey).HasMaxLength(100).IsRequired();
        builder.Property(x => x.ValueType).HasMaxLength(32).IsRequired();
        builder.Property(x => x.ValueJson).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.Reason).HasMaxLength(500).IsRequired();
        builder.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.RuleKey, x.EffectiveFrom });
        builder.HasIndex(x => new { x.TenantId, x.RuleKey, x.EffectiveFrom });
        builder.Ignore(x => x.DomainEvents);
    }
}
