using Kadr.Platform.Domain.Subscriptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kadr.Infrastructure.Persistence.Configurations;

public sealed class SubscriptionConfiguration : IEntityTypeConfiguration<Subscription>
{
    public void Configure(EntityTypeBuilder<Subscription> builder)
    {
        builder.ToTable("subscriptions");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.TenantId).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.Status });
        builder.Ignore(x => x.DomainEvents);
    }
}
