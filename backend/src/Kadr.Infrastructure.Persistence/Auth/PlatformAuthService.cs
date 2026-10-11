using Kadr.Identity.Application.Auth;
using Kadr.Identity.Application.Options;
using Kadr.Identity.Application.Security;
using Kadr.Identity.Domain.Tokens;
using Kadr.Platform.Application.PlatformAdmin;
using Kadr.Platform.Application.Tenants;
using Kadr.SharedKernel.Constants;
using Kadr.SharedKernel.Ids;
using Kadr.SharedKernel.Results;
using Kadr.SharedKernel.Tenancy;
using Kadr.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Kadr.Infrastructure.Persistence.Auth;

public sealed class PlatformAuthService(
    KadrDbContext db,
    IPasswordHasher passwordHasher,
    DummyPasswordHash dummyPasswordHash,
    IJwtTokenService jwtTokenService,
    ITenantContextAccessor tenantContext,
    IClock clock,
    IOptions<JwtOptions> jwtOptions) : IPlatformAuthService
{
    public async Task<Result<PlatformLoginResult>> LoginAsync(
        string email,
        string password,
        string? clientIp,
        string? deviceInfo,
        CancellationToken cancellationToken = default)
    {
        var normalized = EmailNormalizer.Normalize(email);
        var admin = await db.PlatformAdmins
            .SingleOrDefaultAsync(a => a.NormalizedEmail == normalized, cancellationToken)
            .ConfigureAwait(false);

        var hashToVerify = admin?.PasswordHash ?? dummyPasswordHash.Value;
        var valid = passwordHasher.VerifyPassword(hashToVerify, password);

        if (admin is null || !admin.IsActive || !valid)
        {
            return Result.Failure<PlatformLoginResult>(
                ErrorCodes.AuthInvalidCredentials,
                ErrorCodes.AuthInvalidCredentials);
        }

        tenantContext.SetPlatformAdmin(admin.Id);
        admin.RecordLogin(clock.UtcNow);

        var access = jwtTokenService.CreatePlatformAccessToken(admin.Id, admin.PreferredLanguage);
        var opaque = TokenHashing.CreateOpaqueToken();
        var familyId = GuidV7.NewGuid();
        var expiresAt = clock.UtcNow.AddDays(jwtOptions.Value.RefreshTokenDays);

        db.PlatformRefreshTokens.Add(PlatformRefreshToken.Create(
            admin.Id,
            TokenHashing.Hash(opaque),
            familyId,
            expiresAt,
            clientIp,
            deviceInfo));

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success(new PlatformLoginResult(
            access.AccessToken,
            access.ExpiresInSeconds,
            opaque,
            expiresAt,
            admin.Id,
            admin.FullName,
            admin.Email));
    }

    public async Task<Result<PlatformRotatedRefresh>> RefreshAsync(
        string opaqueToken,
        string? clientIp,
        string? deviceInfo,
        CancellationToken cancellationToken = default)
    {
        var hash = TokenHashing.Hash(opaqueToken);
        var existing = await db.PlatformRefreshTokens
            .SingleOrDefaultAsync(t => t.TokenHash == hash, cancellationToken)
            .ConfigureAwait(false);

        if (existing is null)
        {
            return Result.Failure<PlatformRotatedRefresh>(
                ErrorCodes.AuthRefreshInvalid,
                ErrorCodes.AuthRefreshInvalid);
        }

        tenantContext.SetPlatformAdmin(existing.PlatformAdminId);

        if (existing.RevokedAt is not null)
        {
            await RevokeFamilyAsync(existing.FamilyId, cancellationToken).ConfigureAwait(false);
            return Result.Failure<PlatformRotatedRefresh>(
                ErrorCodes.AuthRefreshReuse,
                ErrorCodes.AuthRefreshReuse);
        }

        if (existing.ExpiresAt <= clock.UtcNow)
        {
            existing.Revoke(clock.UtcNow);
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return Result.Failure<PlatformRotatedRefresh>(
                ErrorCodes.AuthRefreshInvalid,
                ErrorCodes.AuthRefreshInvalid);
        }

        var admin = await db.PlatformAdmins
            .SingleOrDefaultAsync(a => a.Id == existing.PlatformAdminId, cancellationToken)
            .ConfigureAwait(false);

        if (admin is null || !admin.IsActive)
        {
            existing.Revoke(clock.UtcNow);
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return Result.Failure<PlatformRotatedRefresh>(
                ErrorCodes.AuthRefreshInvalid,
                ErrorCodes.AuthRefreshInvalid);
        }

        var opaque = TokenHashing.CreateOpaqueToken();
        var expiresAt = clock.UtcNow.AddDays(jwtOptions.Value.RefreshTokenDays);
        var replacement = PlatformRefreshToken.Create(
            admin.Id,
            TokenHashing.Hash(opaque),
            existing.FamilyId,
            expiresAt,
            clientIp,
            deviceInfo);

        existing.Revoke(clock.UtcNow, replacement.Id);
        db.PlatformRefreshTokens.Add(replacement);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success(new PlatformRotatedRefresh(
            admin.Id,
            admin.PreferredLanguage,
            opaque,
            expiresAt));
    }

    public async Task LogoutAsync(string? opaqueToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(opaqueToken))
        {
            return;
        }

        var hash = TokenHashing.Hash(opaqueToken);
        var existing = await db.PlatformRefreshTokens
            .SingleOrDefaultAsync(t => t.TokenHash == hash, cancellationToken)
            .ConfigureAwait(false);

        if (existing is null)
        {
            return;
        }

        tenantContext.SetPlatformAdmin(existing.PlatformAdminId);
        await RevokeFamilyAsync(existing.FamilyId, cancellationToken).ConfigureAwait(false);
    }

    private async Task RevokeFamilyAsync(Guid familyId, CancellationToken cancellationToken)
    {
        var tokens = await db.PlatformRefreshTokens
            .Where(t => t.FamilyId == familyId && t.RevokedAt == null)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var now = clock.UtcNow;
        foreach (var token in tokens)
        {
            token.Revoke(now);
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
