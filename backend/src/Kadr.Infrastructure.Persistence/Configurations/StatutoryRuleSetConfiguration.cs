using Kadr.Platform.Domain.Payroll;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kadr.Infrastructure.Persistence.Configurations;

public sealed class StatutoryRuleSetConfiguration : IEntityTypeConfiguration<StatutoryRuleSet>
{
    public void Configure(EntityTypeBuilder<StatutoryRuleSet> builder)
    {
        builder.ToTable("statutory_rule_sets");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Country).HasMaxLength(2).IsRequired();
        builder.Property(x => x.RuleKey).HasMaxLength(100).IsRequired();
        builder.Property(x => x.ValueType).HasMaxLength(32).IsRequired();
        builder.Property(x => x.ValueJson).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.SourceNote).HasMaxLength(500);
        builder.HasIndex(x => new { x.Country, x.RuleKey, x.EffectiveFrom, x.Version }).IsUnique();
        builder.Ignore(x => x.DomainEvents);
    }
}
