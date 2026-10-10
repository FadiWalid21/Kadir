using Kadr.Identity.Domain.Roles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kadr.Infrastructure.Persistence.Configurations;

public sealed class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> builder)
    {
        builder.ToTable("role_permissions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.PermissionCode).HasMaxLength(128).IsRequired();
        builder.HasIndex(x => new { x.TenantId, x.RoleId, x.PermissionCode }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.RoleId });
    }
}
