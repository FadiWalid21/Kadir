using Kadr.Platform.Domain.Org;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kadr.Infrastructure.Persistence.Configurations;

public sealed class BranchConfiguration : IEntityTypeConfiguration<Branch>
{
    public void Configure(EntityTypeBuilder<Branch> builder)
    {
        builder.ToTable("branches");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.OutOfRangePolicy).HasConversion<string>().HasMaxLength(32);
        builder.HasIndex(x => new { x.TenantId, x.LegalEntityId });
        builder.HasIndex(x => new { x.TenantId, x.IsDefault });
        builder.Ignore(x => x.DomainEvents);
        builder.Ignore(x => x.HasLocationConfigured);
    }
}
