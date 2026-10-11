using Kadr.Identity.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kadr.Infrastructure.Persistence.Configurations;

public sealed class AppUserConfiguration : IEntityTypeConfiguration<AppUser>
{
    public void Configure(EntityTypeBuilder<AppUser> builder)
    {
        builder.ToTable("app_users");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Email).HasMaxLength(320).IsRequired();
        builder.Property(x => x.NormalizedEmail).HasMaxLength(320).IsRequired();
        builder.Property(x => x.PhoneNumber).HasMaxLength(32);
        builder.Property(x => x.PasswordHash).HasMaxLength(512);
        builder.Property(x => x.FullName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.PreferredLanguage).HasMaxLength(8).IsRequired();
        builder.HasIndex(x => new { x.TenantId, x.NormalizedEmail }).IsUnique();
        builder.Ignore(x => x.DomainEvents);
    }
}
