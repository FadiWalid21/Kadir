namespace Kadr.Platform.Application.Abstractions;

/// <summary>Counts active employees for capacity checks. Stub until Core HR (F15).</summary>
public interface IActiveEmployeeCounter
{
    Task<int> CountAsync(Guid tenantId, CancellationToken cancellationToken = default);
}
