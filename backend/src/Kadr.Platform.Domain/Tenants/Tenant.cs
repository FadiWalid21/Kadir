using Kadr.SharedKernel.Domain;
using Kadr.SharedKernel.Ids;

namespace Kadr.Platform.Domain.Tenants;

public sealed class Tenant : AggregateRoot, IPlatformEntity, IAuditable
{
    private Tenant()
    {
    }

    public string Name { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public string IndustryCode { get; private set; } = string.Empty;
    public TenantLanguage DefaultLanguage { get; private set; } = TenantLanguage.Ar;
    public string TimeZone { get; private set; } = "Africa/Cairo";
    public TenantStatus Status { get; private set; } = TenantStatus.Trial;
    public Guid? LogoFileId { get; private set; }
    public string? AccentColor { get; private set; }
    public DateTimeOffset? TrialEndsAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid? CreatedBy { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public Guid? UpdatedBy { get; private set; }

    public static Tenant Create(
        Guid id,
        string name,
        string slug,
        string industryCode,
        TenantLanguage defaultLanguage,
        string timeZone,
        DateTimeOffset trialEndsAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);
        ArgumentException.ThrowIfNullOrWhiteSpace(industryCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(timeZone);

        return new Tenant
        {
            Id = id,
            Name = name.Trim(),
            Slug = slug.Trim().ToLowerInvariant(),
            IndustryCode = industryCode.Trim().ToLowerInvariant(),
            DefaultLanguage = defaultLanguage,
            TimeZone = timeZone.Trim(),
            Status = TenantStatus.Trial,
            TrialEndsAt = trialEndsAt,
            CreatedAt = DateTimeOffset.UtcNow,
        };
    }

    public void Rename(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.Trim();
    }

    public void UpdateDefaults(TenantLanguage defaultLanguage, string timeZone)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(timeZone);
        DefaultLanguage = defaultLanguage;
        TimeZone = timeZone.Trim();
    }

    public void SetLogo(Guid fileId)
    {
        if (fileId == Guid.Empty)
        {
            throw new ArgumentException("Logo file id is required.", nameof(fileId));
        }

        LogoFileId = fileId;
    }

    public void ClearLogo()
    {
        LogoFileId = null;
    }
}
