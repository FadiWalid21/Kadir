using Kadr.SharedKernel.Domain;
using Kadr.SharedKernel.Ids;

namespace Kadr.Platform.Domain.Org;

public sealed class LegalEntity : AggregateRoot, ITenantOwned, IAuditable
{
    private LegalEntity()
    {
    }

    public Guid TenantId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string CountryCode { get; private set; } = "EG";
    public bool IsDefault { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid? CreatedBy { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public Guid? UpdatedBy { get; private set; }

    public static LegalEntity CreateDefault(Guid tenantId, string name, string countryCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(countryCode);

        return new LegalEntity
        {
            Id = GuidV7.NewGuid(),
            TenantId = tenantId,
            Name = name.Trim(),
            CountryCode = countryCode.Trim().ToUpperInvariant(),
            IsDefault = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };
    }
}
