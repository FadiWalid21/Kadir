using Kadr.Identity.Application.Auth;
using Kadr.Identity.Application.Options;
using Kadr.Identity.Application.Security;
using Kadr.Identity.Domain.Tokens;
using Kadr.SharedKernel.Constants;
using Kadr.SharedKernel.Ids;
using Kadr.SharedKernel.Results;
using Kadr.SharedKernel.Tenancy;
using Kadr.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Kadr.Infrastructure.Persistence.Auth;

public sealed class RefreshTokenService(
    KadrDbContext db,
    ITenantContextAccessor tenantContext,
    IPermissionService permissions,
    IClock clock,
    IOptions<JwtOptions> jwtOptions) : IRefreshTokenService
{
    public async Task<IssuedRefreshToken> IssueAsync(
        Guid tenantId,
        Guid userId,
        string? createdByIp,
        string? deviceInfo,
        CancellationToken cancellationToken = default)
    {
        var opaque = TokenHashing.CreateOpaqueToken();
        var familyId = GuidV7.NewGuid();
        var expiresAt = clock.UtcNow.AddDays(jwtOptions.Value.RefreshTokenDays);

        tenantContext.Set(tenantId, userId);
        db.RefreshTokens.Add(RefreshToken.Create(
            tenantId,
            userId,
            TokenHashing.Hash(opaque),
            familyId,
            expiresAt,
            createdByIp,
            deviceInfo));

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new IssuedRefreshToken(opaque, familyId, expiresAt);
    }

    public async Task<Result<RotatedRefreshToken>> RotateAsync(
        string opaqueToken,
        string? createdByIp,
        string? deviceInfo,
        CancellationToken cancellationToken = default)
    {
        var hash = TokenHashing.Hash(opaqueToken);
        // Auth infrastructure: lookup by secret hash before tenant context exists.
        var existing = await db.RefreshTokens
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(t => t.TokenHash == hash, cancellationToken)
            .ConfigureAwait(false);

        if (existing is null)
        {
            return Result.Failure<RotatedRefreshToken>(ErrorCodes.AuthRefreshInvalid, ErrorCodes.AuthRefreshInvalid);
        }

        tenantContext.Set(existing.TenantId, existing.UserId);

        if (existing.RevokedAt is not null)
        {
            await RevokeFamilyAsync(existing.FamilyId, existing.TenantId, cancellationToken).ConfigureAwait(false);
            return Result.Failure<RotatedRefreshToken>(ErrorCodes.AuthRefreshReuse, ErrorCodes.AuthRefreshReuse);
        }

        if (existing.ExpiresAt <= clock.UtcNow)
        {
            existing.Revoke(clock.UtcNow);
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return Result.Failure<RotatedRefreshToken>(ErrorCodes.AuthRefreshInvalid, ErrorCodes.AuthRefreshInvalid);
        }

        var opaque = TokenHashing.CreateOpaqueToken();
        var expiresAt = clock.UtcNow.AddDays(jwtOptions.Value.RefreshTokenDays);
        var replacement = RefreshToken.Create(
            existing.TenantId,
            existing.UserId,
            TokenHashing.Hash(opaque),
            existing.FamilyId,
            expiresAt,
            createdByIp,
            deviceInfo);

        existing.Revoke(clock.UtcNow, replacement.Id);
        db.RefreshTokens.Add(replacement);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var roles = await permissions.GetRoleNamesAsync(existing.TenantId, existing.UserId, cancellationToken)
            .ConfigureAwait(false);
        var language = await db.Users.AsNoTracking()
            .Where(u => u.Id == existing.UserId)
            .Select(u => u.PreferredLanguage)
            .SingleAsync(cancellationToken)
            .ConfigureAwait(false);

        return Result.Success(new RotatedRefreshToken(
            opaque,
            existing.FamilyId,
            expiresAt,
            existing.UserId,
            existing.TenantId,
            roles,
            language));
    }

    public async Task RevokeFamilyByOpaqueTokenAsync(
        string opaqueToken,
        CancellationToken cancellationToken = default)
    {
        var hash = TokenHashing.Hash(opaqueToken);
        var existing = await db.RefreshTokens
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(t => t.TokenHash == hash, cancellationToken)
            .ConfigureAwait(false);

        if (existing is null)
        {
            return;
        }

        tenantContext.Set(existing.TenantId, existing.UserId);
        await RevokeFamilyAsync(existing.FamilyId, existing.TenantId, cancellationToken).ConfigureAwait(false);
    }

    public async Task RevokeAllForUserAsync(
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        tenantContext.Set(tenantId, userId);
        var now = clock.UtcNow;
        var tokens = await db.RefreshTokens
            .Where(t => t.TenantId == tenantId && t.UserId == userId && t.RevokedAt == null)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        foreach (var token in tokens)
        {
            token.Revoke(now);
        }

        if (tokens.Count > 0)
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task RevokeFamilyAsync(Guid familyId, Guid tenantId, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var tokens = await db.RefreshTokens
            .Where(t => t.TenantId == tenantId && t.FamilyId == familyId && t.RevokedAt == null)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var token in tokens)
        {
            token.Revoke(now);
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
