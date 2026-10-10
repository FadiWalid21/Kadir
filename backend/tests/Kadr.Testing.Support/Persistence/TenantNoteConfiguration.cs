using Kadr.Testing.Support.Notes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kadr.Testing.Support.Persistence;

public sealed class TenantNoteConfiguration : IEntityTypeConfiguration<TenantNote>
{
    public void Configure(EntityTypeBuilder<TenantNote> builder)
    {
        builder.ToTable("tenant_notes");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Title).HasMaxLength(200).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.HasIndex(x => new { x.TenantId, x.CreatedAt });
        builder.Ignore(x => x.DomainEvents);
    }
}
