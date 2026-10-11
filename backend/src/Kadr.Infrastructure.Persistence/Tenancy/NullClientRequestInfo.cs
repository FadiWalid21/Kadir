using Kadr.Platform.Application.Abstractions;

namespace Kadr.Infrastructure.Persistence.Tenancy;

public sealed class NullClientRequestInfo : IClientRequestInfo
{
    public string? IpAddress => null;

    public string? UserAgent => null;
}
