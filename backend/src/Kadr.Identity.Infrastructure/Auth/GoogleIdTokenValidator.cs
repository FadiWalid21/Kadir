using Google.Apis.Auth;
using Kadr.Identity.Application.Auth;
using Kadr.Identity.Application.Options;
using Kadr.SharedKernel.Constants;
using Microsoft.Extensions.Options;

namespace Kadr.Identity.Infrastructure.Auth;

public sealed class GoogleIdTokenValidator(IOptions<GoogleAuthOptions> options) : IGoogleIdTokenValidator
{
    public async Task<GoogleIdTokenPayload> ValidateAsync(
        string credential,
        string expectedNonce,
        CancellationToken cancellationToken = default)
    {
        if (!options.Value.IsEnabled)
        {
            throw new GoogleIdTokenValidationException(ErrorCodes.AuthGoogleDisabled);
        }

        if (string.IsNullOrWhiteSpace(credential) || string.IsNullOrWhiteSpace(expectedNonce))
        {
            throw new GoogleIdTokenValidationException(ErrorCodes.AuthGoogleTokenInvalid);
        }

        try
        {
            var settings = new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = [options.Value.ClientId.Trim()],
            };

            var payload = await GoogleJsonWebSignature
                .ValidateAsync(credential, settings)
                .ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(payload.Subject)
                || string.IsNullOrWhiteSpace(payload.Email))
            {
                throw new GoogleIdTokenValidationException(ErrorCodes.AuthGoogleTokenInvalid);
            }

            if (!string.Equals(payload.Nonce, expectedNonce, StringComparison.Ordinal))
            {
                throw new GoogleIdTokenValidationException(ErrorCodes.AuthGoogleNonceInvalid);
            }

            if (!payload.EmailVerified)
            {
                throw new GoogleIdTokenValidationException(ErrorCodes.AuthGoogleEmailUnverified);
            }

            return new GoogleIdTokenPayload(
                payload.Subject,
                payload.Email,
                payload.Name,
                payload.EmailVerified);
        }
        catch (GoogleIdTokenValidationException)
        {
            throw;
        }
        catch (InvalidJwtException)
        {
            throw new GoogleIdTokenValidationException(ErrorCodes.AuthGoogleTokenInvalid);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new GoogleIdTokenValidationException(ErrorCodes.AuthGoogleTokenInvalid, ex);
        }
    }
}
