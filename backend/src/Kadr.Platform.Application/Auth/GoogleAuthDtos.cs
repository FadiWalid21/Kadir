namespace Kadr.Platform.Application.Auth;

public sealed record AuthProvidersResponse(GoogleProviderDto Google);

public sealed record GoogleProviderDto(bool Enabled, string? ClientId);

public sealed record GoogleNonceResponse(string Nonce, DateTimeOffset ExpiresAt);

public sealed record GoogleLoginRequest(string Credential);

public sealed record GoogleSelectTenantRequest(string SelectionToken, Guid TenantId);

public sealed record GoogleCompanyOptionDto(
    Guid TenantId,
    string Name,
    string RoleName,
    DateTimeOffset? LastLoginAt);

public sealed record GoogleLoginSuccessDto(
    string AccessToken,
    int ExpiresInSeconds,
    string TokenType,
    string RefreshOpaqueToken,
    DateTimeOffset RefreshExpiresAt);

public sealed record GoogleTenantSelectionDto(
    string Status,
    string SelectionToken,
    IReadOnlyList<GoogleCompanyOptionDto> Companies);

public sealed record GoogleNotFoundDto(string Code, string RegistrationToken);

public static class GoogleLoginStatuses
{
    public const string TenantSelectionRequired = "tenant_selection_required";
}
