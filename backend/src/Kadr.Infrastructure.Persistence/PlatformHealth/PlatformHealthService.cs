using System.Diagnostics;
using Kadr.Platform.Application.Abstractions;
using Kadr.Platform.Application.Notifications;
using Kadr.Platform.Application.PlatformHealth;
using Kadr.SharedKernel.Abstractions;
using Kadr.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Kadr.Infrastructure.Persistence.PlatformHealth;

/// <summary>
/// Real checks only: the API answering this request, a live database connection, and what the DI container
/// wires for file storage, email, and web push. Storage/email/push have no probe port, so they report the
/// provider kind instead of pretending to have tested delivery. Error details are never returned to the caller.
/// </summary>
public sealed class PlatformHealthService(
    KadrDbContext db,
    IFileStorage fileStorage,
    IEmailSender emailSender,
    IPushNotificationSender pushSender,
    IOptions<WebPushOptions> webPushOptions,
    IClock clock,
    ILogger<PlatformHealthService> logger) : IPlatformHealthService
{
    private const string LocalStorageType = "LocalFileStorage";
    private const string LoggingEmailType = "LoggingEmailSender";
    private const string BrevoEmailType = "BrevoEmailSender";
    private const string WebPushSenderType = "WebPushNotificationSender";
    private const string LoggingPushType = "LoggingPushNotificationSender";
    private static readonly TimeSpan DatabaseTimeout = TimeSpan.FromSeconds(3);

    public async Task<PlatformHealthDto> CheckAsync(CancellationToken cancellationToken = default)
    {
        var components = new List<PlatformHealthComponentDto>
        {
            new(PlatformHealthComponents.Api, PlatformHealthStatuses.Ok, null, null, null),
            await CheckDatabaseAsync(cancellationToken).ConfigureAwait(false),
            DescribeStorage(),
            DescribeEmail(),
            DescribeWebPush(),
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

    private PlatformHealthComponentDto DescribeWebPush()
    {
        var options = webPushOptions.Value;
        var typeName = pushSender.GetType().Name;

        if (!options.Enabled)
        {
            return new PlatformHealthComponentDto(
                PlatformHealthComponents.WebPush,
                PlatformHealthStatuses.Degraded,
                "disabled",
                "webpush_disabled",
                null);
        }

        if (typeName == LoggingPushType || !options.CanSend)
        {
            return new PlatformHealthComponentDto(
                PlatformHealthComponents.WebPush,
                PlatformHealthStatuses.Degraded,
                "logging",
                "not_configured",
                null);
        }

        if (typeName == WebPushSenderType)
        {
            return new PlatformHealthComponentDto(
                PlatformHealthComponents.WebPush,
                PlatformHealthStatuses.Configured,
                "webpush",
                "not_probed",
                null);
        }

        // Test hosts and alternate senders that are wired with keys present.
        return new PlatformHealthComponentDto(
            PlatformHealthComponents.WebPush,
            PlatformHealthStatuses.Configured,
            typeName,
            "not_probed",
            null);
    }
}
