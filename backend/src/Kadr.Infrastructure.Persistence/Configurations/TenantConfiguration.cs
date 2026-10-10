using Kadr.Platform.Domain.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kadr.Infrastructure.Persistence.Configurations;

public sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("tenants");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Slug).HasMaxLength(63).IsRequired();
        builder.Property(x => x.IndustryCode).HasMaxLength(64).IsRequired();
        builder.Property(x => x.TimeZone).HasMaxLength(64).IsRequired();
        builder.Property(x => x.AccentColor).HasMaxLength(16);
        builder.HasIndex(x => x.Slug).IsUnique();
        builder.Ignore(x => x.DomainEvents);
    }
}
