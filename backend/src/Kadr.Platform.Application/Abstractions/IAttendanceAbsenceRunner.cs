namespace Kadr.Platform.Application.Abstractions;

public interface IAttendanceAbsenceRunner
{
    Task RunOnceAsync(CancellationToken cancellationToken = default);
}
