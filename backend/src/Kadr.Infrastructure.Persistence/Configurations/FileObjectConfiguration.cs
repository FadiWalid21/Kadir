using Kadr.Platform.Domain.Files;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kadr.Infrastructure.Persistence.Configurations;

public sealed class FileObjectConfiguration : IEntityTypeConfiguration<FileObject>
{
    public void Configure(EntityTypeBuilder<FileObject> builder)
    {
        builder.ToTable("file_objects");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.StoragePath).HasMaxLength(500).IsRequired();
        builder.Property(x => x.FileName).HasMaxLength(255).IsRequired();
        builder.Property(x => x.ContentType).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Hash).HasMaxLength(128).IsRequired();
        builder.Property(x => x.Purpose).HasConversion<string>().HasMaxLength(32);
        builder.HasIndex(x => new { x.TenantId, x.Purpose });
        builder.HasIndex(x => new { x.TenantId, x.Id });
        builder.Ignore(x => x.DomainEvents);
    }
}
