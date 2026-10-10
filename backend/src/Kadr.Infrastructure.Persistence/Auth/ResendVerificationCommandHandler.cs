using Kadr.Identity.Application.Auth;
using Kadr.Identity.Application.Security;
using Kadr.Identity.Domain.Tokens;
using Kadr.Platform.Application.Tenants;
using Kadr.SharedKernel.Abstractions;
using Kadr.SharedKernel.Application;
using Kadr.SharedKernel.Constants;
using Kadr.SharedKernel.Results;
using Kadr.SharedKernel.Tenancy;
using Kadr.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kadr.Infrastructure.Persistence.Auth;

public sealed class ResendVerificationCommandHandler(
    KadrDbContext db,
    ITenantContextAccessor tenantContext,
    IEmailSender emailSender,
    IClock clock,
    ILogger<ResendVerificationCommandHandler> logger) : ICommandHandler<ResendVerificationCommand>
{
    public async Task<Result> HandleAsync(
        ResendVerificationCommand command,
        CancellationToken cancellationToken = default)
    {
        var slug = command.CompanySlug.Trim().ToLowerInvariant();
        var tenant = await db.Tenants.AsNoTracking()
            .SingleOrDefaultAsync(t => t.Slug == slug, cancellationToken)
            .ConfigureAwait(false);

        // Always succeed outwardly to avoid account enumeration.
        if (tenant is null)
        {
            return Result.Success();
        }

        tenantContext.Set(tenant.Id);
        var normalized = EmailNormalizer.Normalize(command.Email);
        var user = await db.Users
            .SingleOrDefaultAsync(u => u.NormalizedEmail == normalized, cancellationToken)
            .ConfigureAwait(false);

        if (user is null || user.EmailConfirmed)
        {
            return Result.Success();
        }

        var now = clock.UtcNow;
        var existing = await db.EmailVerificationTokens
            .Where(t => t.UserId == user.Id && t.ConsumedAt == null)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var token in existing)
        {
            token.Consume(now);
        }

        var raw = TokenHashing.CreateOpaqueToken();
        db.EmailVerificationTokens.Add(EmailVerificationToken.Create(
            tenant.Id,
            user.Id,
            TokenHashing.Hash(raw),
            now.Add(AuthConstants.EmailVerificationLifetime)));

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await emailSender.SendAsync(
                new EmailMessage(
                    user.Email,
                    "Verify your Kadr email / تأكيد البريد",
                    $"Verify your email with token: {raw}"),
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to resend verification for user {UserId}", user.Id);
        }

        return Result.Success();
    }
}
