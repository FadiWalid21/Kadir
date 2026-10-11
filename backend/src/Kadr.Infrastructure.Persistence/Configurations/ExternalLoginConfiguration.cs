using Kadr.Identity.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kadr.Infrastructure.Persistence.Configurations;

public sealed class ExternalLoginConfiguration : IEntityTypeConfiguration<ExternalLogin>
{
    public void Configure(EntityTypeBuilder<ExternalLogin> builder)
    {
        builder.ToTable("external_logins");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Provider).HasMaxLength(64).IsRequired();
        builder.Property(x => x.ProviderSubject).HasMaxLength(256).IsRequired();
        builder.Property(x => x.Email).HasMaxLength(320).IsRequired();
        builder.HasIndex(x => new { x.TenantId, x.UserId, x.Provider });
        builder.HasIndex(x => new { x.Provider, x.ProviderSubject, x.UserId }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.Provider, x.ProviderSubject });
    }
}
