using Kadr.SharedKernel.Domain;
using Kadr.SharedKernel.Ids;

namespace Kadr.Platform.Domain.Notifications;

public sealed class Notification : AggregateRoot, ITenantOwned
{
    private Notification()
    {
    }

    public Guid TenantId { get; private set; }
    public Guid RecipientUserId { get; private set; }
    public string Type { get; private set; } = string.Empty;
    public string TitleAr { get; private set; } = string.Empty;
    public string TitleEn { get; private set; } = string.Empty;
    public string BodyAr { get; private set; } = string.Empty;
    public string BodyEn { get; private set; } = string.Empty;
    public string? LinkPath { get; private set; }
    public string? MetadataJson { get; private set; }

    /// <summary>False when the user turned the in-app channel off; the row only anchors other channels.</summary>
    public bool InboxVisible { get; private set; } = true;

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? ReadAt { get; private set; }

    public bool IsRead => ReadAt is not null;

    public static Notification Create(
        Guid tenantId,
        Guid recipientUserId,
        string type,
        string titleAr,
        string titleEn,
        string bodyAr,
        string bodyEn,
        string? linkPath,
        string? metadataJson,
        bool inboxVisible = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(type);
        ArgumentException.ThrowIfNullOrWhiteSpace(titleAr);
        ArgumentException.ThrowIfNullOrWhiteSpace(titleEn);
        ArgumentException.ThrowIfNullOrWhiteSpace(bodyAr);
        ArgumentException.ThrowIfNullOrWhiteSpace(bodyEn);
        if (recipientUserId == Guid.Empty)
        {
            throw new ArgumentException("Recipient is required.", nameof(recipientUserId));
        }

        return new Notification
        {
            Id = GuidV7.NewGuid(),
            TenantId = tenantId,
            RecipientUserId = recipientUserId,
            Type = type.Trim(),
            TitleAr = titleAr.Trim(),
            TitleEn = titleEn.Trim(),
            BodyAr = bodyAr.Trim(),
            BodyEn = bodyEn.Trim(),
            LinkPath = string.IsNullOrWhiteSpace(linkPath) ? null : linkPath.Trim(),
            MetadataJson = string.IsNullOrWhiteSpace(metadataJson) ? null : metadataJson,
            InboxVisible = inboxVisible,
            CreatedAt = DateTimeOffset.UtcNow,
        };
    }

    /// <summary>Marks the notification read. Idempotent: the first read time is kept.</summary>
    public void MarkRead() => ReadAt ??= DateTimeOffset.UtcNow;
}
