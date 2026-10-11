using Kadr.Identity.Domain.Tokens;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kadr.Infrastructure.Persistence.Configurations;

public sealed class AuthOneTimeTokenConfiguration : IEntityTypeConfiguration<AuthOneTimeToken>
{
    public void Configure(EntityTypeBuilder<AuthOneTimeToken> builder)
    {
        builder.ToTable("auth_one_time_tokens");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TokenHash).HasMaxLength(128).IsRequired();
        builder.Property(x => x.PayloadJson).HasColumnType("jsonb");
        builder.Property(x => x.Purpose).HasConversion<string>().HasMaxLength(64);
        builder.HasIndex(x => new { x.Purpose, x.TokenHash }).IsUnique();
        builder.HasIndex(x => x.ExpiresAt);
    }
}
