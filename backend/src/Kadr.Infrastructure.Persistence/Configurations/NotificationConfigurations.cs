using Kadr.Platform.Domain.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kadr.Infrastructure.Persistence.Configurations;

public sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("notifications");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Type).HasMaxLength(64).IsRequired();
        builder.Property(x => x.TitleAr).HasMaxLength(200).IsRequired();
        builder.Property(x => x.TitleEn).HasMaxLength(200).IsRequired();
        builder.Property(x => x.BodyAr).HasMaxLength(2000).IsRequired();
        builder.Property(x => x.BodyEn).HasMaxLength(2000).IsRequired();
        builder.Property(x => x.LinkPath).HasMaxLength(500);
        builder.Property(x => x.MetadataJson).HasMaxLength(4000);
        builder.HasIndex(x => new { x.TenantId, x.RecipientUserId, x.CreatedAt });
        builder.HasIndex(x => new { x.TenantId, x.RecipientUserId, x.ReadAt });
        builder.Ignore(x => x.DomainEvents);
        builder.Ignore(x => x.IsRead);
    }
}

public sealed class NotificationPreferenceConfiguration : IEntityTypeConfiguration<NotificationPreference>
{
    public void Configure(EntityTypeBuilder<NotificationPreference> builder)
    {
        builder.ToTable("notification_preferences");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.EventType).HasMaxLength(64).IsRequired();
        builder.HasIndex(x => new { x.TenantId, x.UserId, x.EventType }).IsUnique();
    }
}

public sealed class NotificationDeliveryConfiguration : IEntityTypeConfiguration<NotificationDelivery>
{
    public void Configure(EntityTypeBuilder<NotificationDelivery> builder)
    {
        builder.ToTable("notification_deliveries");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Channel).HasConversion<string>().HasMaxLength(16);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
        builder.Property(x => x.LastError).HasMaxLength(NotificationDelivery.MaxErrorLength);
        builder.HasIndex(x => new { x.TenantId, x.Status, x.NextAttemptAt });
        builder.HasIndex(x => new { x.TenantId, x.NotificationId, x.Channel });
        builder.HasOne<Notification>()
            .WithMany()
            .HasForeignKey(x => x.NotificationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class PushSubscriptionConfiguration : IEntityTypeConfiguration<PushSubscription>
{
    public void Configure(EntityTypeBuilder<PushSubscription> builder)
    {
        builder.ToTable("push_subscriptions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Endpoint).HasMaxLength(PushSubscription.MaxEndpointLength).IsRequired();
        builder.Property(x => x.P256dh).HasMaxLength(PushSubscription.MaxKeyLength).IsRequired();
        builder.Property(x => x.Auth).HasMaxLength(PushSubscription.MaxKeyLength).IsRequired();
        builder.Property(x => x.DeviceLabel).HasMaxLength(PushSubscription.MaxDeviceLabelLength);
        builder.Property(x => x.UserAgent).HasMaxLength(PushSubscription.MaxUserAgentLength);
        builder.Property(x => x.IsActive).HasDefaultValue(true);
        builder.HasIndex(x => new { x.TenantId, x.UserId, x.Endpoint }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.UserId });
        builder.HasIndex(x => new { x.TenantId, x.UserId, x.IsActive });
    }
}
