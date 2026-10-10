using Kadr.Identity.Domain.Roles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kadr.Infrastructure.Persistence.Configurations;

public sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("roles");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.Property(x => x.DescriptionAr).HasMaxLength(500);
        builder.Property(x => x.DescriptionEn).HasMaxLength(500);
        builder.HasIndex(x => new { x.TenantId, x.Name }).IsUnique();
        builder.Ignore(x => x.DomainEvents);
    }
}
