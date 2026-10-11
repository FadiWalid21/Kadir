using Kadr.SharedKernel.Constants;
using Kadr.SharedKernel.Domain;
using Kadr.SharedKernel.Ids;
using Kadr.SharedKernel.Time;

namespace Kadr.Identity.Domain.Users;

public sealed class AppUser : AggregateRoot, ITenantOwned, IAuditable
{
    private AppUser()
    {
    }

    public Guid TenantId { get; private set; }
    public string Email { get; private set; } = string.Empty;
    public string NormalizedEmail { get; private set; } = string.Empty;
    public string? PhoneNumber { get; private set; }
    public string PasswordHash { get; private set; } = string.Empty;
    public string FullName { get; private set; } = string.Empty;
    public string PreferredLanguage { get; private set; } = "ar";
    public bool IsActive { get; private set; } = true;
    public bool EmailConfirmed { get; private set; }
    public DateTimeOffset? LastLoginAt { get; private set; }
    public Guid? EmployeeId { get; private set; }
    public int AccessFailedCount { get; private set; }
    public DateTimeOffset? LockoutEndUtc { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid? CreatedBy { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public Guid? UpdatedBy { get; private set; }

    public static AppUser CreateOwner(
        Guid tenantId,
        string email,
        string normalizedEmail,
        string passwordHash,
        string fullName,
        string preferredLanguage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(normalizedEmail);
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(fullName);

        return new AppUser
        {
            Id = GuidV7.NewGuid(),
            TenantId = tenantId,
            Email = email,
            NormalizedEmail = normalizedEmail,
            PasswordHash = passwordHash,
            FullName = fullName.Trim(),
            PreferredLanguage = preferredLanguage,
            IsActive = true,
            EmailConfirmed = false,
            CreatedAt = DateTimeOffset.UtcNow,
        };
    }

    public bool IsLockedOut(DateTimeOffset utcNow) =>
        LockoutEndUtc is { } end && end > utcNow;

    public bool RegisterFailedLogin(IClock clock)
    {
        AccessFailedCount++;
        UpdatedAt = clock.UtcNow;

        if (AccessFailedCount < AuthConstants.LockoutFailedAttemptThreshold)
        {
            return false;
        }

        LockoutEndUtc = clock.UtcNow.Add(AuthConstants.LockoutDuration);
        return true;
    }

    public void ResetAccessFailedCount(IClock clock)
    {
        AccessFailedCount = 0;
        LockoutEndUtc = null;
        UpdatedAt = clock.UtcNow;
    }

    public void MarkEmailConfirmed(IClock clock)
    {
        EmailConfirmed = true;
        UpdatedAt = clock.UtcNow;
    }

    public void RecordSuccessfulLogin(IClock clock)
    {
        ResetAccessFailedCount(clock);
        LastLoginAt = clock.UtcNow;
    }

    public static AppUser CreateInvited(
        Guid tenantId,
        string email,
        string normalizedEmail,
        string passwordHash,
        string fullName,
        string preferredLanguage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(normalizedEmail);
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(fullName);

        return new AppUser
        {
            Id = GuidV7.NewGuid(),
            TenantId = tenantId,
            Email = email,
            NormalizedEmail = normalizedEmail,
            PasswordHash = passwordHash,
            FullName = fullName.Trim(),
            PreferredLanguage = preferredLanguage,
            IsActive = true,
            EmailConfirmed = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };
    }

    public void Rename(string fullName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullName);
        FullName = fullName.Trim();
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void SetActive(bool isActive)
    {
        IsActive = isActive;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
