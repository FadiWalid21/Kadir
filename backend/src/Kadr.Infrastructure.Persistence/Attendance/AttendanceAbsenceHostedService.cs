using Kadr.Platform.Application.Abstractions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Kadr.Infrastructure.Persistence.Attendance;

public sealed class AttendanceAbsenceHostedService(
    IAttendanceAbsenceRunner runner,
    IHostEnvironment environment,
    ILogger<AttendanceAbsenceHostedService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (environment.IsEnvironment("Testing"))
        {
            logger.LogInformation("Attendance absence hosted service disabled in Testing");
            return;
        }

        await runner.RunOnceAsync(stoppingToken).ConfigureAwait(false);

        using var timer = new PeriodicTimer(Interval);
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            try
            {
                await runner.RunOnceAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Attendance absence job failed");
            }
        }
    }
}
