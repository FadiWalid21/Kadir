using Kadr.Identity.Application.Options;
using Kadr.Identity.Application.Security;
using Kadr.Identity.Domain.PlatformAdmins;
using Kadr.Platform.Application.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Kadr.Infrastructure.Persistence.Seeding;

/// <summary>
/// Seeds the first platform owner from configuration. Password is never logged.
/// Skips when Email/Password are empty (local machines without secrets).
/// </summary>
public sealed class PlatformAdminSeeder(
    KadrDbContext db,
    IPasswordHasher passwordHasher,
    IOptions<PlatformAdminOptions> options,
    ILogger<PlatformAdminSeeder> logger)
{
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var opts = options.Value;
        if (string.IsNullOrWhiteSpace(opts.Email) || string.IsNullOrWhiteSpace(opts.Password))
        {
            logger.LogInformation("PlatformAdmin seed skipped (Email/Password not configured).");
            return;
        }

        var normalized = EmailNormalizer.Normalize(opts.Email);
        var existing = await db.PlatformAdmins
            .SingleOrDefaultAsync(a => a.NormalizedEmail == normalized, cancellationToken)
            .ConfigureAwait(false);

        if (existing is not null)
        {
            return;
        }

        var hash = passwordHasher.HashPassword(opts.Password);
        var name = string.IsNullOrWhiteSpace(opts.FullName) ? "Platform Owner" : opts.FullName.Trim();
        db.PlatformAdmins.Add(PlatformAdmin.Create(
            opts.Email.Trim(),
            normalized,
            hash,
            name));

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        logger.LogInformation("Seeded platform admin for {Email}", normalized);
    }
}
