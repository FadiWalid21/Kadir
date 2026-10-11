using Kadr.Platform.Application.Abstractions;

namespace Kadr.Api.Http;

public sealed class HttpClientRequestInfo(IHttpContextAccessor accessor) : IClientRequestInfo
{
    public string? IpAddress =>
        accessor.HttpContext?.Connection.RemoteIpAddress?.ToString();

    public string? UserAgent
    {
        get
        {
            var ua = accessor.HttpContext?.Request.Headers.UserAgent.ToString();
            return string.IsNullOrWhiteSpace(ua) ? null : ua;
        }
    }
}
