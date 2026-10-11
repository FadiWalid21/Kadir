using Kadr.SharedKernel.Domain;
using Kadr.SharedKernel.Ids;

namespace Kadr.Identity.Domain.Users;

public sealed class ExternalLogin : Entity, ITenantOwned
{
    private ExternalLogin()
    {
    }

    public Guid TenantId { get; private set; }
    public Guid UserId { get; private set; }
    public string Provider { get; private set; } = string.Empty;
    public string ProviderSubject { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }

    public static ExternalLogin Create(
        Guid tenantId,
        Guid userId,
        string provider,
        string providerSubject,
        string email)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerSubject);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        return new ExternalLogin
        {
            Id = GuidV7.NewGuid(),
            TenantId = tenantId,
            UserId = userId,
            Provider = provider.Trim(),
            ProviderSubject = providerSubject.Trim(),
            Email = email.Trim(),
            CreatedAt = DateTimeOffset.UtcNow,
        };
    }

    public void UpdateEmail(string email)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        Email = email.Trim();
    }
}
