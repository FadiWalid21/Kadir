using System.Security.Claims;

namespace Kadr.Identity.Application.Auth;

public sealed record AccessTokenResult(string AccessToken, int ExpiresInSeconds);

public interface IJwtTokenService
{
    AccessTokenResult CreateAccessToken(
        Guid userId,
        Guid tenantId,
        IReadOnlyCollection<string> roles,
        string language);

    /// <summary>JWT for platform operators: no tenant_id; claim platform_admin=true.</summary>
    AccessTokenResult CreatePlatformAccessToken(Guid platformAdminId, string language);

    ClaimsPrincipal? ValidateAccessToken(string token);
}
