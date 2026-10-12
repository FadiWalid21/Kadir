using System.Net;
using System.Net.Sockets;
using Kadr.Platform.Application.Abstractions;
using Kadr.Platform.Application.Notifications;
using Kadr.SharedKernel.Constants;
using Kadr.SharedKernel.Results;
using Microsoft.Extensions.Options;

namespace Kadr.Platform.Infrastructure.Push;

/// <summary>Rejects non-HTTPS and non-allow-listed push endpoints (SSRF hardening).</summary>
public sealed class PushEndpointValidator(IOptions<WebPushOptions> options) : IPushEndpointValidator
{
    private static readonly string[] DefaultAllowedHostSuffixes =
    [
        "fcm.googleapis.com",
        "android.googleapis.com",
        "updates.push.services.mozilla.com",
        "push.services.mozilla.com",
        "web.push.apple.com",
        "wns.windows.com",
        "notify.windows.com",
    ];

    public Result Validate(string endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint)
            || !Uri.TryCreate(endpoint.Trim(), UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps
            || !string.IsNullOrEmpty(uri.UserInfo)
            || uri.IsDefaultPort is false && uri.Port is not 443)
        {
            return Result.Failure(ErrorCodes.NotificationsPushEndpointRejected, ErrorCodes.NotificationsPushEndpointRejected);
        }

        var host = uri.IdnHost;
        if (string.IsNullOrWhiteSpace(host)
            || IPAddress.TryParse(host, out _)
            || IsLocalOrPrivateHost(host)
            || !IsAllowedHost(host))
        {
            return Result.Failure(ErrorCodes.NotificationsPushEndpointRejected, ErrorCodes.NotificationsPushEndpointRejected);
        }

        return Result.Success();
    }

    private bool IsAllowedHost(string host)
    {
        var suffixes = options.Value.AllowedEndpointHosts is { Length: > 0 }
            ? options.Value.AllowedEndpointHosts
            : DefaultAllowedHostSuffixes;

        foreach (var suffix in suffixes)
        {
            if (string.IsNullOrWhiteSpace(suffix))
            {
                continue;
            }

            var normalized = suffix.Trim().TrimStart('.').ToLowerInvariant();
            if (host.Equals(normalized, StringComparison.OrdinalIgnoreCase)
                || host.EndsWith("." + normalized, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsLocalOrPrivateHost(string host)
    {
        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".local", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".internal", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!IPAddress.TryParse(host, out var ip))
        {
            return false;
        }

        if (IPAddress.IsLoopback(ip)
            || ip.Equals(IPAddress.Any)
            || ip.Equals(IPAddress.IPv6Any)
            || ip.AddressFamily == AddressFamily.InterNetworkV6 && ip.IsIPv6LinkLocal)
        {
            return true;
        }

        if (ip.AddressFamily == AddressFamily.InterNetwork)
        {
            var bytes = ip.GetAddressBytes();
            return bytes[0] == 10
                   || (bytes[0] == 172 && bytes[1] is >= 16 and <= 31)
                   || (bytes[0] == 192 && bytes[1] == 168)
                   || (bytes[0] == 169 && bytes[1] == 254)
                   || bytes[0] == 127;
        }

        return false;
    }
}
