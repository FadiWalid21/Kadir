using System.Diagnostics;
using Kadr.Platform.Application.Abstractions;
using Kadr.Platform.Application.PlatformHealth;
using Kadr.SharedKernel.Abstractions;
using Kadr.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kadr.Infrastructure.Persistence.PlatformHealth;

/// <summary>
/// Real checks only: the API answering this request, a live database connection, and what the DI container
/// wires for file storage and email. Storage and email have no probe port, so they report the provider kind
/// instead of pretending to have tested delivery. Error details are never returned to the caller.
/// </summary>
public sealed class PlatformHealthService(
    KadrDbContext db,
    IFileStorage fileStorage,
    IEmailSender emailSender,
    IClock clock,
    ILogger<PlatformHealthService> logger) : IPlatformHealthService
{
    private const string LocalStorageType = "LocalFileStorage";
    private const string LoggingEmailType = "LoggingEmailSender";
    private const string BrevoEmailType = "BrevoEmailSender";
    private static readonly TimeSpan DatabaseTimeout = TimeSpan.FromSeconds(3);

    public async Task<PlatformHealthDto> CheckAsync(CancellationToken cancellationToken = default)
    {
        var components = new List<PlatformHealthComponentDto>
        {
            new(PlatformHealthComponents.Api, PlatformHealthStatuses.Ok, null, null, null),
            await CheckDatabaseAsync(cancellationToken).ConfigureAwait(false),
            DescribeStorage(),
            DescribeEmail(),
        };

        var overall = components.Any(c => c.Status == PlatformHealthStatuses.Down)
            ? PlatformHealthStatuses.Down
            : components.Any(c => c.Status == PlatformHealthStatuses.Degraded)
                ? PlatformHealthStatuses.Degraded
                : PlatformHealthStatuses.Ok;

        return new PlatformHealthDto(overall, clock.UtcNow, components);
    }

    private async Task<PlatformHealthComponentDto> CheckDatabaseAsync(CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(DatabaseTimeout);
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var canConnect = await db.Database.CanConnectAsync(timeout.Token).ConfigureAwait(false);
            stopwatch.Stop();
            return new PlatformHealthComponentDto(
                PlatformHealthComponents.Database,
                canConnect ? PlatformHealthStatuses.Ok : PlatformHealthStatuses.Down,
                "postgresql",
                canConnect ? null : "unreachable",
                stopwatch.ElapsedMilliseconds);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return DatabaseDown("timeout", stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Database health check failed");
            return DatabaseDown("unreachable", stopwatch.ElapsedMilliseconds);
        }
    }

    private static PlatformHealthComponentDto DatabaseDown(string detail, long elapsedMs) =>
        new(PlatformHealthComponents.Database, PlatformHealthStatuses.Down, "postgresql", detail, elapsedMs);

    private PlatformHealthComponentDto DescribeStorage()
    {
        var typeName = fileStorage.GetType().Name;
        return typeName == LocalStorageType
            ? new PlatformHealthComponentDto(
                PlatformHealthComponents.Storage,
                PlatformHealthStatuses.Degraded,
                "local",
                "local_disk_not_durable",
                null)
            : new PlatformHealthComponentDto(
                PlatformHealthComponents.Storage,
                PlatformHealthStatuses.Configured,
                typeName,
                "not_probed",
                null);
    }

    private PlatformHealthComponentDto DescribeEmail()
    {
        var typeName = emailSender.GetType().Name;
        if (typeName == BrevoEmailType)
        {
            return new PlatformHealthComponentDto(
                PlatformHealthComponents.Email,
                PlatformHealthStatuses.Configured,
                "brevo",
                "not_probed",
                null);
        }

        return typeName == LoggingEmailType
            ? new PlatformHealthComponentDto(
                PlatformHealthComponents.Email,
                PlatformHealthStatuses.Degraded,
                "logging",
                "messages_logged_not_delivered",
                null)
            : new PlatformHealthComponentDto(
                PlatformHealthComponents.Email,
                PlatformHealthStatuses.Configured,
                typeName,
                "not_probed",
                null);
    }
}
