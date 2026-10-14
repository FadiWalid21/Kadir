using System.Net;
using System.Text.Json;
using Kadr.Platform.Application.Email;
using Kadr.Platform.Application.Notifications;
using Kadr.SharedKernel.Abstractions;
using Lib.Net.Http.WebPush.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using LibPushMessage = Lib.Net.Http.WebPush.PushMessage;
using LibPushSubscription = Lib.Net.Http.WebPush.PushSubscription;
using LibPushServiceClient = Lib.Net.Http.WebPush.PushServiceClient;
using LibPushServiceClientException = Lib.Net.Http.WebPush.PushServiceClientException;
using LibPushMessageUrgency = Lib.Net.Http.WebPush.PushMessageUrgency;

namespace Kadr.Platform.Infrastructure.Push;

/// <summary>Sends Web Push via VAPID. Never logs endpoint or key material.</summary>
public sealed class WebPushNotificationSender : IPushNotificationSender, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly WebPushOptions _options;
    private readonly string _iconUrl;
    private readonly ILogger<WebPushNotificationSender> _logger;
    private readonly LibPushServiceClient _client;
    private readonly VapidAuthentication? _vapid;

    public WebPushNotificationSender(
        IOptions<WebPushOptions> options,
        IOptions<AppOptions> appOptions,
        IHttpClientFactory httpClientFactory,
        ILogger<WebPushNotificationSender> logger)
    {
        _options = options.Value;
        _logger = logger;
        _iconUrl = ResolveIconUrl(appOptions.Value.PublicWebBaseUrl);
        _client = new LibPushServiceClient(httpClientFactory.CreateClient(nameof(WebPushNotificationSender)))
        {
            AutoRetryAfter = false,
            DefaultTimeToLive = 86_400,
        };

        if (_options.CanSend)
        {
            _vapid = new VapidAuthentication(_options.VapidPublicKey, _options.VapidPrivateKey)
            {
                Subject = _options.Subject,
            };
            _client.DefaultAuthentication = _vapid;
            _client.DefaultAuthenticationScheme = VapidAuthenticationScheme.Vapid;
        }
    }

    public async Task<PushSendOutcome> SendAsync(
        PushTarget target,
        PushMessage message,
        CancellationToken cancellationToken = default)
    {
        if (!_options.CanSend || _vapid is null)
        {
            return PushSendOutcome.NotConfigured;
        }

        var payload = JsonSerializer.Serialize(
            new
            {
                notification = new
                {
                    title = message.Title,
                    body = message.Body,
                    icon = _iconUrl,
                    badge = _iconUrl,
                    dir = message.Dir,
                    lang = message.Lang,
                    tag = message.Tag,
                    data = new
                    {
                        onActionClick = new
                        {
                            @default = new { operation = "openWindow", url = message.LinkPath ?? "/" },
                        },
                    },
                },
                title = message.Title,
                body = message.Body,
                data = new { url = message.LinkPath ?? "/" },
                tag = message.Tag,
                lang = message.Lang,
                dir = message.Dir,
            },
            JsonOptions);

        var subscription = new LibPushSubscription
        {
            Endpoint = target.Endpoint,
        };
        subscription.Keys["p256dh"] = target.P256dh;
        subscription.Keys["auth"] = target.Auth;

        var pushMessage = new LibPushMessage(payload)
        {
            Urgency = LibPushMessageUrgency.Normal,
            TimeToLive = 86_400,
            Topic = message.Tag,
        };

        try
        {
            await _client
                .RequestPushMessageDeliveryAsync(subscription, pushMessage, cancellationToken)
                .ConfigureAwait(false);
            _logger.LogInformation("Web push outcome=Sent status=201");
            return PushSendOutcome.Sent;
        }
        catch (LibPushServiceClientException ex)
        {
            var status = (int)ex.StatusCode;
            if (ex.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone)
            {
                _logger.LogInformation("Web push outcome=SubscriptionGone status={StatusCode}", status);
                return PushSendOutcome.SubscriptionGone;
            }

            _logger.LogWarning("Web push outcome=Failed status={StatusCode}", status);
            return PushSendOutcome.Failed;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Web push outcome=Failed");
            return PushSendOutcome.Failed;
        }
    }

    public void Dispose()
    {
        _vapid?.Dispose();
    }

    private static string ResolveIconUrl(string? publicWebBaseUrl)
    {
        const string relative = "/brand/icon-192.png";
        if (string.IsNullOrWhiteSpace(publicWebBaseUrl))
        {
            return relative;
        }

        return $"{publicWebBaseUrl.TrimEnd('/')}{relative}";
    }
}
