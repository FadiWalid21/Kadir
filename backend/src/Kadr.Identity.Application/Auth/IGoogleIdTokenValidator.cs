namespace Kadr.Identity.Application.Auth;

public sealed record GoogleIdTokenPayload(
    string Subject,
    string Email,
    string? Name,
    bool EmailVerified);

public interface IGoogleIdTokenValidator
{
    Task<GoogleIdTokenPayload> ValidateAsync(
        string credential,
        string expectedNonce,
        CancellationToken cancellationToken = default);
}
