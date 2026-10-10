using Kadr.Identity.Domain.Tokens;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kadr.Infrastructure.Persistence.Configurations;

public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TokenHash).HasMaxLength(128).IsRequired();
        builder.Property(x => x.CreatedByIp).HasMaxLength(64);
        builder.Property(x => x.DeviceInfo).HasMaxLength(256);
        builder.HasIndex(x => new { x.TenantId, x.TokenHash }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.FamilyId });
        builder.HasIndex(x => new { x.TenantId, x.UserId });
    }
}
