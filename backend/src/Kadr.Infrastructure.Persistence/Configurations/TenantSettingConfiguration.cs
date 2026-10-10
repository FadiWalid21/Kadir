using Kadr.Platform.Domain.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kadr.Infrastructure.Persistence.Configurations;

public sealed class TenantSettingConfiguration : IEntityTypeConfiguration<TenantSetting>
{
    public void Configure(EntityTypeBuilder<TenantSetting> builder)
    {
        builder.ToTable("tenant_settings");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Key).HasMaxLength(128).IsRequired();
        builder.Property(x => x.ValueJson).HasColumnType("jsonb").IsRequired();
        builder.HasIndex(x => new { x.TenantId, x.Key }).IsUnique();
        builder.Ignore(x => x.DomainEvents);
    }
}
