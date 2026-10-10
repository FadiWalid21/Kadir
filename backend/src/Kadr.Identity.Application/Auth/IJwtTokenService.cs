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

    ClaimsPrincipal? ValidateAccessToken(string token);
}
