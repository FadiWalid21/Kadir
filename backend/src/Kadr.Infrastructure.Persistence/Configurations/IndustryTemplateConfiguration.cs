using Kadr.Platform.Domain.Templates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kadr.Infrastructure.Persistence.Configurations;

public sealed class IndustryTemplateConfiguration : IEntityTypeConfiguration<IndustryTemplate>
{
    public void Configure(EntityTypeBuilder<IndustryTemplate> builder)
    {
        builder.ToTable("industry_templates");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(64).IsRequired();
        builder.Property(x => x.NameAr).HasMaxLength(200).IsRequired();
        builder.Property(x => x.NameEn).HasMaxLength(200).IsRequired();
        builder.Property(x => x.DescriptionAr).HasMaxLength(500);
        builder.Property(x => x.DescriptionEn).HasMaxLength(500);
        builder.Property(x => x.PayloadJson).HasColumnType("jsonb").IsRequired();
        builder.HasIndex(x => new { x.Code, x.Version }).IsUnique();
        builder.Ignore(x => x.DomainEvents);
    }
}
