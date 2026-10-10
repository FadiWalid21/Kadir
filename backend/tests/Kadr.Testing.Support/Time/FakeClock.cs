using Kadr.SharedKernel.Time;

namespace Kadr.Testing.Support.Time;

public sealed class FakeClock : IClock
{
    public DateTimeOffset UtcNow { get; set; } = DateTimeOffset.UtcNow;

    public void Advance(TimeSpan by) => UtcNow = UtcNow.Add(by);
}
