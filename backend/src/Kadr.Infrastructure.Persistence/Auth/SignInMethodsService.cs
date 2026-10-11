using System.Text.Json;
using Kadr.Identity.Application.Auth;
using Kadr.Identity.Application.Options;
using Kadr.Identity.Application.Security;
using Kadr.Identity.Domain.Tokens;
using Kadr.Identity.Domain.Users;
using Kadr.Platform.Application.Abstractions;
using Kadr.Platform.Application.Auth;
using Kadr.Platform.Application.Tenants;
using Kadr.SharedKernel.Abstractions;
using Kadr.SharedKernel.Constants;
using Kadr.SharedKernel.Results;
using Kadr.SharedKernel.Tenancy;
using Kadr.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Kadr.Infrastructure.Persistence.Auth;

/// <summary>
/// Manages how the signed-in user can authenticate (password, Google). Tenant and user ids come only
/// from <see cref="ITenantContext"/>. Every change revokes the user's other sessions and sends a
/// security notice after the data is committed.
/// </summary>
public sealed class SignInMethodsService(
    KadrDbContext db,
    ITenantContext tenantContext,
    IOptions<GoogleAuthOptions> googleOptions,
    IGoogleIdTokenValidator tokenValidator,
    IPasswordHasher passwordHasher,
    ICommonPasswordList commonPasswords,
    IRefreshTokenService refreshTokenService,
    IAuditLogWriter audit,
    IEmailSender emailSender,
    IClock clock,
    ILogger<SignInMethodsService> logger) : ISignInMethodsService
{
    public async Task<Result<SignInMethodsResponse>> GetAsync(CancellationToken cancellationToken = default)
    {
        if (!TryGetIdentity(out var userId))
        {
            return Result.Failure<SignInMethodsResponse>(ErrorCodes.UsersNotFound, ErrorCodes.UsersNotFound);
        }

        var user = await db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new { HasPassword = u.PasswordHash != null && u.PasswordHash != string.Empty })
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (user is null)
        {
            return Result.Failure<SignInMethodsResponse>(ErrorCodes.UsersNotFound, ErrorCodes.UsersNotFound);
        }

        var googleEmail = await db.ExternalLogins.AsNoTracking()
            .Where(l => l.UserId == userId && l.Provider == ExternalLoginProviders.Google)
            .Select(l => l.Email)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return Result.Success(new SignInMethodsResponse(
            new PasswordMethodDto(user.HasPassword),
            new GoogleMethodDto(googleEmail is not null, googleEmail)));
    }

    public async Task<Result> LinkGoogleAsync(
        string credential,
        string? refreshOpaqueToken,
        CancellationToken cancellationToken = default)
    {
        if (!googleOptions.Value.IsEnabled)
        {
            return Result.Failure(ErrorCodes.AuthGoogleDisabled, ErrorCodes.AuthGoogleDisabled);
        }

        if (!TryGetIdentity(out var userId))
        {
            return Result.Failure(ErrorCodes.UsersNotFound, ErrorCodes.UsersNotFound);
        }

        var tenantId = tenantContext.TenantId!.Value;

        var payloadResult = await ValidateCredentialAsync(credential, cancellationToken).ConfigureAwait(false);
        if (payloadResult.IsFailure)
        {
            return Result.Failure(payloadResult.ErrorCode!, payloadResult.ErrorMessage!);
        }

        var payload = payloadResult.Value!;

        var user = await db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new { u.Email })
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (user is null)
        {
            return Result.Failure(ErrorCodes.UsersNotFound, ErrorCodes.UsersNotFound);
        }

        var alreadyLinked = await db.ExternalLogins
            .AnyAsync(
                l => l.Provider == ExternalLoginProviders.Google
                     && (l.UserId == userId || l.ProviderSubject == payload.Subject),
                cancellationToken)
            .ConfigureAwait(false);
        if (alreadyLinked)
        {
            return Result.Failure(ErrorCodes.AuthGoogleAlreadyLinked, ErrorCodes.AuthGoogleAlreadyLinked);
        }

        db.ExternalLogins.Add(ExternalLogin.Create(
            tenantId,
            userId,
            ExternalLoginProviders.Google,
            payload.Subject,
            payload.Email));
        audit.Stage(
            tenantId,
            userId,
            AuditActions.AuthGoogleLinked,
            "Auth",
            userId,
            null,
            JsonSerializer.Serialize(new { googleEmail = EmailNormalizer.Normalize(payload.Email) }));
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await RevokeOtherSessionsAsync(tenantId, userId, refreshOpaqueToken, cancellationToken).ConfigureAwait(false);
        await SendSecurityNoticeAsync(
                user.Email,
                "Google sign-in was linked to your Kadr account / تم ربط تسجيل الدخول بجوجل بحسابك",
                "Google sign-in was linked to your Kadr account. If this was not you, reset your password immediately.\n"
                + "تم ربط تسجيل الدخول بجوجل بحسابك في كادر. إذا لم تكن أنت، فأعد تعيين كلمة المرور فوراً.",
                userId,
                cancellationToken)
            .ConfigureAwait(false);

        return Result.Success();
    }

    public async Task<Result> UnlinkGoogleAsync(
        string? refreshOpaqueToken,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetIdentity(out var userId))
        {
            return Result.Failure(ErrorCodes.UsersNotFound, ErrorCodes.UsersNotFound);
        }

        var tenantId = tenantContext.TenantId!.Value;
        var user = await db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new { u.Email, HasPassword = u.PasswordHash != null && u.PasswordHash != string.Empty })
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (user is null)
        {
            return Result.Failure(ErrorCodes.UsersNotFound, ErrorCodes.UsersNotFound);
        }

        var links = await db.ExternalLogins
            .Where(l => l.UserId == userId && l.Provider == ExternalLoginProviders.Google)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        if (links.Count == 0)
        {
            return Result.Success();
        }

        if (!user.HasPassword)
        {
            return Result.Failure(ErrorCodes.AuthLastSignInMethod, ErrorCodes.AuthLastSignInMethod);
        }

        db.ExternalLogins.RemoveRange(links);
        audit.Stage(
            tenantId,
            userId,
            AuditActions.AuthGoogleUnlinked,
            "Auth",
            userId,
            null,
            null);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await RevokeOtherSessionsAsync(tenantId, userId, refreshOpaqueToken, cancellationToken).ConfigureAwait(false);
        await SendSecurityNoticeAsync(
                user.Email,
                "Google sign-in was removed from your Kadr account / تمت إزالة تسجيل الدخول بجوجل من حسابك",
                "Google sign-in was removed from your Kadr account. If this was not you, reset your password immediately.\n"
                + "تمت إزالة تسجيل الدخول بجوجل من حسابك في كادر. إذا لم تكن أنت، فأعد تعيين كلمة المرور فوراً.",
                userId,
                cancellationToken)
            .ConfigureAwait(false);

        return Result.Success();
    }

    public async Task<Result> SetPasswordAsync(
        string? currentPassword,
        string newPassword,
        string? refreshOpaqueToken,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetIdentity(out var userId))
        {
            return Result.Failure(ErrorCodes.UsersNotFound, ErrorCodes.UsersNotFound);
        }

        var tenantId = tenantContext.TenantId!.Value;
        var user = await db.Users
            .SingleOrDefaultAsync(u => u.Id == userId, cancellationToken)
            .ConfigureAwait(false);
        if (user is null)
        {
            return Result.Failure(ErrorCodes.UsersNotFound, ErrorCodes.UsersNotFound);
        }

        var hadPassword = user.HasPassword;
        if (hadPassword
            && (string.IsNullOrEmpty(currentPassword)
                || !passwordHasher.VerifyPassword(user.PasswordHash!, currentPassword)))
        {
            return Result.Failure(ErrorCodes.AuthInvalidCredentials, ErrorCodes.AuthInvalidCredentials);
        }

        var passwordError = PasswordRules.Validate(newPassword, commonPasswords.Passwords);
        if (passwordError is not null)
        {
            return Result.Failure(passwordError, passwordError);
        }

        user.SetPassword(passwordHasher.HashPassword(newPassword), clock);
        audit.Stage(
            tenantId,
            userId,
            hadPassword ? AuditActions.AuthPasswordChanged : AuditActions.AuthPasswordAdded,
            "Auth",
            userId,
            null,
            null);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await RevokeOtherSessionsAsync(tenantId, userId, refreshOpaqueToken, cancellationToken).ConfigureAwait(false);
        await SendSecurityNoticeAsync(
                user.Email,
                hadPassword
                    ? "Your Kadr password was changed / تم تغيير كلمة المرور"
                    : "A password was added to your Kadr account / تمت إضافة كلمة مرور لحسابك",
                "The sign-in settings of your Kadr account changed. If this was not you, reset your password immediately.\n"
                + "تغيّرت إعدادات تسجيل الدخول لحسابك في كادر. إذا لم تكن أنت، فأعد تعيين كلمة المرور فوراً.",
                userId,
                cancellationToken)
            .ConfigureAwait(false);

        return Result.Success();
    }

    private bool TryGetIdentity(out Guid userId)
    {
        userId = tenantContext.UserId ?? Guid.Empty;
        return tenantContext.TenantId is not null && userId != Guid.Empty;
    }

    private async Task<Result<GoogleIdTokenPayload>> ValidateCredentialAsync(
        string credential,
        CancellationToken cancellationToken)
    {
        string? rawNonce;
        try
        {
            rawNonce = GoogleCredentialReader.ReadUnverifiedNonce(credential);
        }
        catch (Exception ex) when (ex is FormatException or JsonException or ArgumentException)
        {
            return Result.Failure<GoogleIdTokenPayload>(
                ErrorCodes.AuthGoogleTokenInvalid,
                ErrorCodes.AuthGoogleTokenInvalid);
        }

        if (string.IsNullOrWhiteSpace(rawNonce))
        {
            return Result.Failure<GoogleIdTokenPayload>(
                ErrorCodes.AuthGoogleNonceInvalid,
                ErrorCodes.AuthGoogleNonceInvalid);
        }

        // Burn the one-time nonce before validating the token so a captured credential cannot be replayed.
        var nonceHash = TokenHashing.Hash(rawNonce);
        var nonceRow = await db.AuthOneTimeTokens
            .SingleOrDefaultAsync(
                t => t.Purpose == AuthOneTimeTokenPurpose.GoogleNonce && t.TokenHash == nonceHash,
                cancellationToken)
            .ConfigureAwait(false);
        if (nonceRow is null || !nonceRow.TryConsume(clock.UtcNow))
        {
            return Result.Failure<GoogleIdTokenPayload>(
                ErrorCodes.AuthGoogleNonceInvalid,
                ErrorCodes.AuthGoogleNonceInvalid);
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
            return Result.Failure<GoogleIdTokenPayload>(ex.ErrorCode, ex.ErrorCode);
        }

        return payload.EmailVerified
            ? Result.Success(payload)
            : Result.Failure<GoogleIdTokenPayload>(
                ErrorCodes.AuthGoogleEmailUnverified,
                ErrorCodes.AuthGoogleEmailUnverified);
    }

    /// <summary>
    /// Keeps the caller's own session (refresh cookie family) and revokes the rest. Without a usable
    /// cookie every session of the user is revoked.
    /// </summary>
    private async Task RevokeOtherSessionsAsync(
        Guid tenantId,
        Guid userId,
        string? refreshOpaqueToken,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(refreshOpaqueToken))
        {
            var familyId = await refreshTokenService
                .ResolveActiveFamilyIdAsync(tenantId, userId, refreshOpaqueToken, cancellationToken)
                .ConfigureAwait(false);
            if (familyId is { } family)
            {
                await refreshTokenService
                    .RevokeAllExceptFamilyAsync(tenantId, userId, family, cancellationToken)
                    .ConfigureAwait(false);
                return;
            }
        }

        await refreshTokenService.RevokeAllForUserAsync(tenantId, userId, cancellationToken).ConfigureAwait(false);
    }

    private async Task SendSecurityNoticeAsync(
        string to,
        string subject,
        string body,
        Guid userId,
        CancellationToken cancellationToken)
    {
        try
        {
            await emailSender.SendAsync(new EmailMessage(to, subject, body), cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to send sign-in security notice for user {UserId}", userId);
        }
    }
}
