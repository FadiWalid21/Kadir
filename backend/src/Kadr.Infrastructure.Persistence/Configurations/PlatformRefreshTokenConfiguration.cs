using Kadr.Identity.Domain.Tokens;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kadr.Infrastructure.Persistence.Configurations;

public sealed class PlatformRefreshTokenConfiguration : IEntityTypeConfiguration<PlatformRefreshToken>
{
    public void Configure(EntityTypeBuilder<PlatformRefreshToken> builder)
    {
        builder.ToTable("platform_refresh_tokens");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TokenHash).HasMaxLength(128).IsRequired();
        builder.Property(x => x.CreatedByIp).HasMaxLength(64);
        builder.Property(x => x.DeviceInfo).HasMaxLength(512);
        builder.HasIndex(x => x.TokenHash).IsUnique();
        builder.HasIndex(x => new { x.PlatformAdminId, x.FamilyId });
    }
}