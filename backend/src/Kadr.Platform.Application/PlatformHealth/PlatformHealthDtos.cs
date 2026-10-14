namespace Kadr.Platform.Application.PlatformHealth;

/// <summary>
/// Component states. "configured" means a real provider is wired but no live probe exists for it;
/// "unknown" means the provider is not recognized so nothing can be said about it.
/// </summary>
public static class PlatformHealthStatuses
{
    public const string Ok = "ok";
    public const string Degraded = "degraded";
    public const string Down = "down";
    public const string Configured = "configured";
    public const string Unknown = "unknown";
}

public static class PlatformHealthComponents
{
    public const string Api = "api";
    public const string Database = "database";
    public const string Storage = "storage";
    public const string Email = "email";
    public const string WebPush = "webpush";
}

public sealed record PlatformHealthComponentDto(
    string Name,
    string Status,
    string? Provider,
    string? Detail,
    long? LatencyMs);

public sealed record PlatformHealthDto(
    string Status,
    DateTimeOffset CheckedAt,
    IReadOnlyList<PlatformHealthComponentDto> Components);
