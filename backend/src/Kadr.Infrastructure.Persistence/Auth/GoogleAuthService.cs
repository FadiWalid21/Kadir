using System.Text.Json;
using Kadr.Identity.Application.Auth;
using Kadr.Identity.Application.Options;
using Kadr.Identity.Application.Security;
using Kadr.Identity.Domain.Tokens;
using Kadr.Identity.Domain.Users;
using Kadr.Platform.Application.Abstractions;
using Kadr.Platform.Application.Auth;
using Kadr.Platform.Application.Tenants;
using Kadr.SharedKernel.Constants;
using Kadr.SharedKernel.Results;
using Kadr.SharedKernel.Tenancy;
using Kadr.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Kadr.Infrastructure.Persistence.Auth;

public sealed class GoogleAuthService(
    KadrDbContext db,
    IOptions<GoogleAuthOptions> googleOptions,
    IGoogleIdTokenValidator tokenValidator,
    IGoogleAccountResolver accountResolver,
    ITenantContextAccessor tenantContext,
    IJwtTokenService jwtTokenService,
    IRefreshTokenService refreshTokenService,
    IPermissionService permissionService,
    IAuditLogWriter audit,
    IClock clock,
    ILogger<GoogleAuthService> logger) : IGoogleAuthService
{
    public AuthProvidersResponse GetProviders()
    {
        var enabled = googleOptions.Value.IsEnabled;
        return new AuthProvidersResponse(
            new GoogleProviderDto(enabled, enabled ? googleOptions.Value.ClientId.Trim() : null));
    }

    public async Task<Result<GoogleNonceResponse>> IssueNonceAsync(CancellationToken cancellationToken = default)
    {
        if (!googleOptions.Value.IsEnabled)
        {
            return Result.Failure<GoogleNonceResponse>(ErrorCodes.AuthGoogleDisabled, ErrorCodes.AuthGoogleDisabled);
        }

        var raw = TokenHashing.CreateOpaqueToken();
        var expires = clock.UtcNow.Add(AuthConstants.GoogleNonceLifetime);
        db.AuthOneTimeTokens.Add(AuthOneTimeToken.Create(
            AuthOneTimeTokenPurpose.GoogleNonce,
            TokenHashing.Hash(raw),
            null,
            expires));
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(new GoogleNonceResponse(raw, expires));
    }

    public async Task<Result<object>> LoginAsync(
        string credential,
        string? clientIp,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        if (!googleOptions.Value.IsEnabled)
        {
            return Result.Failure<object>(ErrorCodes.AuthGoogleDisabled, ErrorCodes.AuthGoogleDisabled);
        }

        // Peek nonce from JWT without full validation first is hard; validate requires nonce.
        // Client must have issued nonce via GIS; we extract nonce claim after a soft parse, or
        // require clients to send nonce separately. Plan: GIS initialize uses our nonce; token contains it.
        // ValidateAsync needs expectedNonce — extract from unverified JWT payload.
        string? rawNonce;
        try
        {
            rawNonce = GoogleCredentialReader.ReadUnverifiedNonce(credential);
        }
        catch
        {
            return Result.Failure<object>(ErrorCodes.AuthGoogleTokenInvalid, ErrorCodes.AuthGoogleTokenInvalid);
        }

        if (string.IsNullOrWhiteSpace(rawNonce))
        {
            return Result.Failure<object>(ErrorCodes.AuthGoogleNonceInvalid, ErrorCodes.AuthGoogleNonceInvalid);
        }

        var nonceHash = TokenHashing.Hash(rawNonce);
        var nonceRow = await db.AuthOneTimeTokens
            .SingleOrDefaultAsync(
                t => t.Purpose == AuthOneTimeTokenPurpose.GoogleNonce && t.TokenHash == nonceHash,
                cancellationToken)
            .ConfigureAwait(false);

        if (nonceRow is null || !nonceRow.TryConsume(clock.UtcNow))
        {
            return Result.Failure<object>(ErrorCodes.AuthGoogleNonceInvalid, ErrorCodes.AuthGoogleNonceInvalid);
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        GoogleIdTokenPayload payload;
        try
        {
            payload = await tokenValidator.ValidateAsync(credential, rawNonce, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (GoogleIdTokenValidationException ex)
        {
            return Result.Failure<object>(ex.ErrorCode, ex.ErrorCode);
        }

        if (!payload.EmailVerified)
        {
            return Result.Failure<object>(ErrorCodes.AuthGoogleEmailUnverified, ErrorCodes.AuthGoogleEmailUnverified);
        }

        var normalizedEmail = EmailNormalizer.Normalize(payload.Email);
        var resolution = await accountResolver
            .ResolveAsync(payload.Subject, normalizedEmail, cancellationToken)
            .ConfigureAwait(false);

        if (resolution.HasUnconfirmedEmailMatch && resolution.ActiveCandidates.Count == 0)
        {
            return Result.Failure<object>(ErrorCodes.AuthEmailNotVerified, ErrorCodes.AuthEmailNotVerified);
        }

        if (resolution.HasOnlyInactiveMatches)
        {
            return Result.Failure<object>(ErrorCodes.AuthAccountDisabled, ErrorCodes.AuthAccountDisabled);
        }

        if (resolution.ActiveCandidates.Count == 0)
        {
            var regToken = await IssueRegistrationTokenAsync(payload, cancellationToken).ConfigureAwait(false);
            return Result.Success<object>(new GoogleNotFoundDto(ErrorCodes.AuthGoogleAccountNotFound, regToken));
        }

        if (resolution.ActiveCandidates.Count > 1)
        {
            var selection = await IssueSelectionTokenAsync(payload, resolution.ActiveCandidates, cancellationToken)
                .ConfigureAwait(false);
            return Result.Success<object>(selection);
        }

        var candidate = resolution.ActiveCandidates[0];
        var success = await CompleteLoginAsync(
                candidate.TenantId,
                candidate.UserId,
                payload,
                clientIp,
                userAgent,
                cancellationToken)
            .ConfigureAwait(false);
        return Result.Success<object>(success);
    }

    public async Task<Result<GoogleLoginSuccessDto>> SelectTenantAsync(
        string selectionToken,
        Guid tenantId,
        string? clientIp,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        if (!googleOptions.Value.IsEnabled)
        {
            return Result.Failure<GoogleLoginSuccessDto>(ErrorCodes.AuthGoogleDisabled, ErrorCodes.AuthGoogleDisabled);
        }

        var hash = TokenHashing.Hash(selectionToken.Trim());
        var row = await db.AuthOneTimeTokens
            .SingleOrDefaultAsync(
                t => t.Purpose == AuthOneTimeTokenPurpose.GoogleSelection && t.TokenHash == hash,
                cancellationToken)
            .ConfigureAwait(false);

        if (row is null || !row.TryConsume(clock.UtcNow) || string.IsNullOrWhiteSpace(row.PayloadJson))
        {
            return Result.Failure<GoogleLoginSuccessDto>(
                ErrorCodes.AuthGoogleSelectionInvalid,
                ErrorCodes.AuthGoogleSelectionInvalid);
        }

        var payload = JsonSerializer.Deserialize<SelectionPayload>(row.PayloadJson);
        if (payload is null || !payload.TenantIds.Contains(tenantId))
        {
            return Result.Failure<GoogleLoginSuccessDto>(
                ErrorCodes.AuthGoogleSelectionInvalid,
                ErrorCodes.AuthGoogleSelectionInvalid);
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var linkedUserId = await db.ExternalLogins.IgnoreQueryFilters().AsNoTracking()
            .Where(l => l.TenantId == tenantId
                        && l.Provider == ExternalLoginProviders.Google
                        && l.ProviderSubject == payload.Subject)
            .Select(l => (Guid?)l.UserId)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        var user = await db.Users.IgnoreQueryFilters()
            .SingleOrDefaultAsync(
                u => u.TenantId == tenantId
                     && u.IsActive
                     && ((linkedUserId != null && u.Id == linkedUserId)
                         || (u.NormalizedEmail == payload.NormalizedEmail && u.EmailConfirmed)),
                cancellationToken)
            .ConfigureAwait(false);

        if (user is null)
        {
            return Result.Failure<GoogleLoginSuccessDto>(
                ErrorCodes.AuthGoogleSelectionInvalid,
                ErrorCodes.AuthGoogleSelectionInvalid);
        }

        var googlePayload = new GoogleIdTokenPayload(payload.Subject, payload.Email, payload.Name, true);
        return Result.Success(await CompleteLoginAsync(
                user.TenantId,
                user.Id,
                googlePayload,
                clientIp,
                userAgent,
                cancellationToken)
            .ConfigureAwait(false));
    }

    private async Task<GoogleLoginSuccessDto> CompleteLoginAsync(
        Guid tenantId,
        Guid userId,
        GoogleIdTokenPayload payload,
        string? clientIp,
        string? userAgent,
        CancellationToken cancellationToken)
    {
        tenantContext.Set(tenantId, userId);
        var user = await db.Users.SingleAsync(u => u.Id == userId, cancellationToken).ConfigureAwait(false);
        user.RecordSuccessfulLogin(clock);

        var existing = await db.ExternalLogins
            .SingleOrDefaultAsync(
                l => l.UserId == userId
                     && l.Provider == ExternalLoginProviders.Google
                     && l.ProviderSubject == payload.Subject,
                cancellationToken)
            .ConfigureAwait(false);

        if (existing is null)
        {
            db.ExternalLogins.Add(ExternalLogin.Create(
                tenantId,
                userId,
                ExternalLoginProviders.Google,
                payload.Subject,
                payload.Email));
        }
        else
        {
            existing.UpdateEmail(payload.Email);
        }

        audit.Stage(
            tenantId,
            userId,
            AuditActions.AuthLoginSucceeded,
            "Auth",
            userId,
            null,
            JsonSerializer.Serialize(new
            {
                email = EmailNormalizer.Normalize(payload.Email),
                method = "google",
            }),
            clientIp,
            userAgent);

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var roles = await permissionService.GetRoleNamesAsync(tenantId, userId, cancellationToken)
            .ConfigureAwait(false);
        var access = jwtTokenService.CreateAccessToken(userId, tenantId, roles, user.PreferredLanguage);
        var refresh = await refreshTokenService
            .IssueAsync(tenantId, userId, clientIp, userAgent, cancellationToken)
            .ConfigureAwait(false);

        logger.LogInformation("User {UserId} logged in with Google for tenant {TenantId}", userId, tenantId);

        return new GoogleLoginSuccessDto(
            access.AccessToken,
            access.ExpiresInSeconds,
            "Bearer",
            refresh.OpaqueToken,
            refresh.ExpiresAt);
    }

    private async Task<string> IssueRegistrationTokenAsync(
        GoogleIdTokenPayload payload,
        CancellationToken cancellationToken)
    {
        var raw = TokenHashing.CreateOpaqueToken();
        var expires = clock.UtcNow.Add(AuthConstants.GoogleRegistrationLifetime);
        var body = JsonSerializer.Serialize(new GoogleRegistrationPayload(
            payload.Subject,
            payload.Email,
            EmailNormalizer.Normalize(payload.Email),
            payload.Name));
        db.AuthOneTimeTokens.Add(AuthOneTimeToken.Create(
            AuthOneTimeTokenPurpose.GoogleRegistration,
            TokenHashing.Hash(raw),
            body,
            expires));
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return raw;
    }

    private async Task<GoogleTenantSelectionDto> IssueSelectionTokenAsync(
        GoogleIdTokenPayload payload,
        IReadOnlyList<GoogleCandidate> candidates,
        CancellationToken cancellationToken)
    {
        var raw = TokenHashing.CreateOpaqueToken();
        var expires = clock.UtcNow.Add(AuthConstants.GoogleSelectionLifetime);
        var body = JsonSerializer.Serialize(new SelectionPayload(
            payload.Subject,
            payload.Email,
            EmailNormalizer.Normalize(payload.Email),
            payload.Name,
            candidates.Select(c => c.TenantId).ToList()));
        db.AuthOneTimeTokens.Add(AuthOneTimeToken.Create(
            AuthOneTimeTokenPurpose.GoogleSelection,
            TokenHashing.Hash(raw),
            body,
            expires));
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new GoogleTenantSelectionDto(
            GoogleLoginStatuses.TenantSelectionRequired,
            raw,
            candidates
                .Select(c => new GoogleCompanyOptionDto(c.TenantId, c.TenantName, c.RoleName, c.LastLoginAt))
                .ToList());
    }

    private sealed record SelectionPayload(
        string Subject,
        string Email,
        string NormalizedEmail,
        string? Name,
        List<Guid> TenantIds);
}
