using Kadr.SharedKernel.Domain;
using Kadr.SharedKernel.Ids;

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
}
