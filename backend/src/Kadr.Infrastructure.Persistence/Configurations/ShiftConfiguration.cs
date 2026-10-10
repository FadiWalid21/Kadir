using Kadr.Platform.Domain.Templates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kadr.Infrastructure.Persistence.Configurations;

public sealed class ShiftConfiguration : IEntityTypeConfiguration<Shift>
{
    public void Configure(EntityTypeBuilder<Shift> builder)
    {
        builder.ToTable("shifts");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.NameAr).HasMaxLength(200).IsRequired();
        builder.Property(x => x.NameEn).HasMaxLength(200).IsRequired();
        builder.Property(x => x.SearchName).HasMaxLength(420).IsRequired();
        builder.Property(x => x.SourceKey).HasMaxLength(100);
        builder.HasIndex(x => new { x.TenantId, x.SourceKey })
            .IsUnique()
            .HasFilter("source_key IS NOT NULL");
        builder.HasIndex(x => new { x.TenantId, x.SearchName });
        builder.HasIndex(x => new { x.TenantId, x.IsActive });
        builder.Ignore(x => x.DomainEvents);
    }
}
