using Kadr.SharedKernel.Domain;
using Kadr.SharedKernel.Ids;

namespace Kadr.Platform.Domain.Settings;

public sealed class TenantSetting : AggregateRoot, ITenantOwned, IAuditable
{
    private TenantSetting()
    {
    }

    public Guid TenantId { get; private set; }
    public string Key { get; private set; } = string.Empty;
    public string ValueJson { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid? CreatedBy { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public Guid? UpdatedBy { get; private set; }

    public static TenantSetting Create(Guid tenantId, string key, string valueJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        return new TenantSetting
        {
            Id = GuidV7.NewGuid(),
            TenantId = tenantId,
            Key = key.Trim(),
            ValueJson = valueJson,
            CreatedAt = DateTimeOffset.UtcNow,
        };
    }

    public void SetValue(string valueJson)
    {
        ValueJson = valueJson ?? string.Empty;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
