using Kadr.SharedKernel.Abstractions;

namespace Kadr.Testing.Support.Push;

public sealed class CapturingPushNotificationSender : IPushNotificationSender
{
    private readonly List<(PushTarget Target, PushMessage Message)> _sent = [];

    public IReadOnlyList<(PushTarget Target, PushMessage Message)> Sent => _sent;

    public PushMessage? LastMessage => _sent.LastOrDefault().Message;

    /// <summary>When set, the next SendAsync returns this outcome instead of Sent.</summary>
    public Queue<PushSendOutcome> NextOutcomes { get; } = new();

    public void Clear()
    {
        _sent.Clear();
        NextOutcomes.Clear();
    }

    public Task<PushSendOutcome> SendAsync(
        PushTarget target,
        PushMessage message,
        CancellationToken cancellationToken = default)
    {
        _sent.Add((target, message));
        var outcome = NextOutcomes.Count > 0 ? NextOutcomes.Dequeue() : PushSendOutcome.Sent;
        return Task.FromResult(outcome);
    }
}
