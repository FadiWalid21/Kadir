namespace Kadr.Platform.Application.Abstractions;

public interface IClientRequestInfo
{
    string? IpAddress { get; }

    string? UserAgent { get; }
}
