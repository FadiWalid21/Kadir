using Kadr.Platform.Domain.Payroll;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kadr.Infrastructure.Persistence.Configurations;

public sealed class PayComponentConfiguration : IEntityTypeConfiguration<PayComponent>
{
    public void Configure(EntityTypeBuilder<PayComponent> builder)
    {
        builder.ToTable("pay_components");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Key).HasMaxLength(64).IsRequired();
        builder.Property(x => x.NameAr).HasMaxLength(200).IsRequired();
        builder.Property(x => x.NameEn).HasMaxLength(200).IsRequired();
        builder.Property(x => x.SearchName).HasMaxLength(420).IsRequired();
        builder.Property(x => x.Kind).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.CalcType).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.SourceKey).HasMaxLength(100);
        builder.HasIndex(x => new { x.TenantId, x.Key }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.SearchName });
        builder.HasIndex(x => new { x.TenantId, x.IsActive });
        builder.Ignore(x => x.DomainEvents);
    }
}
