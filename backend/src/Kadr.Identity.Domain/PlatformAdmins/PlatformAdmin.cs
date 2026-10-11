using Kadr.SharedKernel.Domain;
using Kadr.SharedKernel.Ids;

namespace Kadr.Identity.Domain.PlatformAdmins;

/// <summary>Platform operator (not a tenant user). Seeded from configuration.</summary>
public sealed class PlatformAdmin : AggregateRoot, IPlatformEntity, IAuditable
{
    private PlatformAdmin()
    {
    }

    public string Email { get; private set; } = string.Empty;
    public string NormalizedEmail { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public string FullName { get; private set; } = string.Empty;
    public string PreferredLanguage { get; private set; } = "ar";
    public bool IsActive { get; private set; } = true;
    public DateTimeOffset? LastLoginAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid? CreatedBy { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public Guid? UpdatedBy { get; private set; }

    public static PlatformAdmin Create(
        string email,
        string normalizedEmail,
        string passwordHash,
        string fullName,
        string preferredLanguage = "ar")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(normalizedEmail);
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(fullName);

        return new PlatformAdmin
        {
            Id = GuidV7.NewGuid(),
            Email = email.Trim(),
            NormalizedEmail = normalizedEmail,
            PasswordHash = passwordHash,
            FullName = fullName.Trim(),
            PreferredLanguage = preferredLanguage,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };
    }

    public void RecordLogin(DateTimeOffset utcNow)
    {
        LastLoginAt = utcNow;
        UpdatedAt = utcNow;
    }

    public void ReplacePasswordHash(string passwordHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);
        PasswordHash = passwordHash;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
