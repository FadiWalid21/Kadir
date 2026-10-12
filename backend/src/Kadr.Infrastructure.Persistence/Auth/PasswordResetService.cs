using System.Text.Json;
using Kadr.Identity.Application.Auth;
using Kadr.Identity.Application.Options;
using Kadr.Identity.Application.Security;
using Kadr.Identity.Domain.Tokens;
using Kadr.Platform.Application.Abstractions;
using Kadr.Platform.Application.Email;
using Kadr.Platform.Application.Tenants;
using Kadr.SharedKernel.Abstractions;
using Kadr.SharedKernel.Constants;
using Kadr.SharedKernel.Results;
using Kadr.SharedKernel.Tenancy;
using Kadr.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Kadr.Infrastructure.Persistence.Auth;

/// <summary>
/// Forgot / reset password flow.
/// <para>
/// IgnoreQueryFilters allow-list: this service looks up users by email across every tenant (forgot
/// password has no tenant context yet) and password reset tokens by hash (the token carries the tenant).
/// Both lookups are read-only discovery steps; every write runs after the ambient tenant has been set
/// to the owning tenant and is therefore stamped and filtered normally.
/// </para>
/// </summary>
public sealed class PasswordResetService(
    KadrDbContext db,
    ITenantContextAccessor tenantContext,
    IPasswordHasher passwordHasher,
    ICommonPasswordList commonPasswords,
    IRefreshTokenService refreshTokenService,
    IAuditLogWriter audit,
    IEmailSender emailSender,
    IMemoryCache cache,
    IOptions<PasswordResetOptions> options,
    IOptions<AppOptions> appOptions,
    IClock clock,
    ILogger<PasswordResetService> logger) : IPasswordResetService
{
    private const string ThrottleKeyPrefix = "pwd-reset:email:";

    public async Task<Result> RequestAsync(
        string email,
        string? clientIp,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email) || email.Length > 320)
        {
            return Result.Success();
        }

        var normalized = EmailNormalizer.Normalize(email);
        if (IsThrottled(normalized))
        {
            logger.LogInformation("Password reset request throttled for an email address");
            return Result.Success();
        }

        var accounts = await (
                from user in db.Users.IgnoreQueryFilters().AsNoTracking()
                join tenant in db.Tenants.AsNoTracking() on user.TenantId equals tenant.Id
                where user.NormalizedEmail == normalized && user.IsActive
                orderby tenant.Name
                select new ResetAccount(user.TenantId, user.Id, user.Email, tenant.Name))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (accounts.Count == 0)
        {
            return Result.Success();
        }

        var now = clock.UtcNow;
        var issued = new List<(string CompanyName, string RawToken)>(accounts.Count);

        // One SaveChanges per account: each row belongs to a different tenant and the tenant stamp is per context.
        foreach (var account in accounts)
        {
            tenantContext.Set(account.TenantId, account.UserId);

            var open = await db.PasswordResetTokens
                .Where(t => t.UserId == account.UserId && t.ConsumedAt == null)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            foreach (var token in open)
            {
                token.Invalidate(now);
            }

            var raw = TokenHashing.CreateOpaqueToken();
            db.PasswordResetTokens.Add(PasswordResetToken.Create(
                account.TenantId,
                account.UserId,
                TokenHashing.Hash(raw),
                now.Add(AuthConstants.PasswordResetLifetime)));
            audit.Stage(
                account.TenantId,
                account.UserId,
                AuditActions.AuthPasswordResetRequested,
                "Auth",
                account.UserId,
                null,
                JsonSerializer.Serialize(new { email = normalized }),
                clientIp,
                userAgent);
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            issued.Add((account.CompanyName, raw));
        }

        tenantContext.Clear();

        await SendResetEmailAsync(accounts[0].Email, issued, cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    public async Task<Result> ResetAsync(
        string token,
        string newPassword,
        string? clientIp,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return Result.Failure(ErrorCodes.AuthPasswordResetInvalid, ErrorCodes.AuthPasswordResetInvalid);
        }

        var hash = TokenHashing.Hash(token.Trim());
        var now = clock.UtcNow;
        var row = await db.PasswordResetTokens
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleOrDefaultAsync(t => t.TokenHash == hash, cancellationToken)
            .ConfigureAwait(false);

        if (row is null || row.ConsumedAt is not null || row.ExpiresAt <= now)
        {
            return Result.Failure(ErrorCodes.AuthPasswordResetInvalid, ErrorCodes.AuthPasswordResetInvalid);
        }

        // Checked after the token so an invalid token never reveals password policy, and before consuming
        // so a weak password does not burn a valid token.
        var passwordError = PasswordRules.Validate(newPassword ?? string.Empty, commonPasswords.Passwords);
        if (passwordError is not null)
        {
            return Result.Failure(passwordError, passwordError);
        }

        tenantContext.Set(row.TenantId, row.UserId);
        var user = await db.Users
            .SingleOrDefaultAsync(u => u.Id == row.UserId && u.IsActive, cancellationToken)
            .ConfigureAwait(false);
        if (user is null)
        {
            return Result.Failure(ErrorCodes.AuthPasswordResetInvalid, ErrorCodes.AuthPasswordResetInvalid);
        }

        var email = user.Email;
        var userId = user.Id;
        var tenantId = user.TenantId;

        await using var transaction = await db.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);
        try
        {
            // Atomic single-use consume; also closes any other open reset tokens of the user.
            var consumed = await db.PasswordResetTokens
                .Where(t => t.Id == row.Id && t.ConsumedAt == null && t.ExpiresAt > now)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.ConsumedAt, now), cancellationToken)
                .ConfigureAwait(false);
            if (consumed == 0)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return Result.Failure(ErrorCodes.AuthPasswordResetInvalid, ErrorCodes.AuthPasswordResetInvalid);
            }

            await db.PasswordResetTokens
                .Where(t => t.UserId == userId && t.ConsumedAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.ConsumedAt, now), cancellationToken)
                .ConfigureAwait(false);

            // Works for accounts without a password (Google-only): the reset adds one.
            user.SetPassword(passwordHasher.HashPassword(newPassword!), clock);
            user.MarkEmailConfirmed(clock);
            user.ResetAccessFailedCount(clock);
            audit.Stage(
                tenantId,
                userId,
                AuditActions.AuthPasswordResetCompleted,
                "Auth",
                userId,
                null,
                null,
                clientIp,
                userAgent);
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            await refreshTokenService.RevokeAllForUserAsync(tenantId, userId, cancellationToken)
                .ConfigureAwait(false);

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }

        try
        {
            await emailSender.SendAsync(
                    new EmailMessage(
                        email,
                        "Your Kadr password was reset / تمت إعادة تعيين كلمة المرور",
                        "The password of your Kadr account was reset and all sessions were signed out. "
                        + "If this was not you, contact support immediately.\n"
                        + "تمت إعادة تعيين كلمة مرور حسابك في كادر وتسجيل الخروج من جميع الجلسات. "
                        + "إذا لم تكن أنت، تواصل مع الدعم فوراً."),
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to send password reset notice for user {UserId}", userId);
        }

        return Result.Success();
    }

    private bool IsThrottled(string normalizedEmail)
    {
        var limit = options.Value.EmailPermitLimit;
        if (limit <= 0)
        {
            return false;
        }

        var key = ThrottleKeyPrefix + TokenHashing.Hash(normalizedEmail);
        var count = cache.GetOrCreate(
            key,
            entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(Math.Max(1, options.Value.EmailWindowMinutes));
                return new Counter();
            })!;

        return Interlocked.Increment(ref count.Value) > limit;
    }

    private async Task SendResetEmailAsync(
        string to,
        IReadOnlyList<(string CompanyName, string RawToken)> issued,
        CancellationToken cancellationToken)
    {
        var baseUrl = appOptions.Value.PublicWebBaseUrl;
        var rows = issued
            .Select(i => (
                i.CompanyName,
                i.RawToken,
                PublicWebLinks.TryBuild(baseUrl, PublicWebLinks.ResetPasswordPath, i.RawToken)))
            .ToList();

        try
        {
            await emailSender
                .SendAsync(AuthEmailBodies.PasswordReset(to, rows), cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to send password reset email");
        }
    }

    private sealed record ResetAccount(Guid TenantId, Guid UserId, string Email, string CompanyName);

    private sealed class Counter
    {
        public int Value;
    }
}
