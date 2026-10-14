using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kadr.Infrastructure.Persistence;
using Kadr.Infrastructure.Persistence.Notifications;
using Kadr.Platform.Application.Abstractions;
using Kadr.Platform.Application.Notifications;
using Kadr.Platform.Domain.Notifications;
using Kadr.Platform.Domain.Workflows;
using Kadr.SharedKernel.Abstractions;
using Kadr.SharedKernel.Constants;
using Kadr.SharedKernel.Tenancy;
using Kadr.TenantIsolation.Tests.Auth;
using Kadr.TenantIsolation.Tests.Register;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kadr.TenantIsolation.Tests.Notifications;

/// <summary>
/// Manual browser matrix (18b): Chrome desktop closed tab; Android Chrome closed;
/// iOS 16.4+ installed PWA closed; permission denied; unsupported (Firefox desktop may vary);
/// logout then login as another user (subscription must not leak). Use npm run pwa:serve / HTTPS.
/// </summary>
[Collection(RegisterCollection.Name)]
public sealed class PushSubscriptionApiTests(RegisterApiFactory factory)
{
    private static readonly JsonSerializerOptions JsonOptions = AuthTestHelper.JsonOptions;
    private const string FcmEndpoint = "https://fcm.googleapis.com/fcm/send/kadr-test-endpoint-1";

    [Fact]
    public async Task Upsert_list_delete_and_isolation()
    {
        await factory.ResetDatabaseAsync();
        var helper = new AuthTestHelper(factory);
        var a = await helper.RegisterVerifiedAsync("Push Co A", "push-a@example.com");
        var b = await helper.RegisterVerifiedAsync("Push Co B", "push-b@example.com");
        var sessionA = await helper.LoginSessionAsync(a.Email, AuthTestHelper.Password, a.Slug);
        var sessionB = await helper.LoginSessionAsync(b.Email, AuthTestHelper.Password, b.Slug);
        var clientA = helper.Authed(sessionA.AccessToken);
        var clientB = helper.Authed(sessionB.AccessToken);

        var vapid = await clientA.GetFromJsonAsync<VapidPublicKeyDto>(
            "/api/v1/me/push-subscriptions/vapid-public-key",
            JsonOptions);
        Assert.False(string.IsNullOrWhiteSpace(vapid!.PublicKey));

        var upsert = await clientA.PostAsJsonAsync(
            "/api/v1/me/push-subscriptions",
            new UpsertPushSubscriptionRequest(
                FcmEndpoint,
                new PushKeysDto("p256dh-key-value", "auth-key-value"),
                "Chrome"));
        Assert.Equal(HttpStatusCode.OK, upsert.StatusCode);
        var device = (await upsert.Content.ReadFromJsonAsync<PushSubscriptionDeviceDto>(JsonOptions))!;
        Assert.Equal("Chrome", device.DeviceLabel);
        Assert.True(device.IsActive);
        Assert.DoesNotContain("fcm.googleapis", await upsert.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);

        var list = await clientA.GetFromJsonAsync<PushSubscriptionListDto>(
            "/api/v1/me/push-subscriptions",
            JsonOptions);
        Assert.Single(list!.Items);
        Assert.Equal(device.Id, list.Items[0].Id);

        var listB = await clientB.GetFromJsonAsync<PushSubscriptionListDto>(
            "/api/v1/me/push-subscriptions",
            JsonOptions);
        Assert.Empty(listB!.Items);

        var crossDelete = await clientB.DeleteAsync($"/api/v1/me/push-subscriptions/{device.Id}");
        Assert.Equal(HttpStatusCode.NotFound, crossDelete.StatusCode);

        var delete = await clientA.DeleteAsync($"/api/v1/me/push-subscriptions/{device.Id}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        list = await clientA.GetFromJsonAsync<PushSubscriptionListDto>(
            "/api/v1/me/push-subscriptions",
            JsonOptions);
        Assert.Empty(list!.Items);
    }

    [Fact]
    public async Task Rejects_ssrf_endpoint_and_enforces_max_10()
    {
        await factory.ResetDatabaseAsync();
        var helper = new AuthTestHelper(factory);
        var account = await helper.RegisterVerifiedAsync("Push Limit Co", "push-limit@example.com");
        var session = await helper.LoginSessionAsync(account.Email, AuthTestHelper.Password, account.Slug);
        var client = helper.Authed(session.AccessToken);

        var ssrf = await client.PostAsJsonAsync(
            "/api/v1/me/push-subscriptions",
            new UpsertPushSubscriptionRequest(
                "https://127.0.0.1/push",
                new PushKeysDto("p256dh", "auth")));
        Assert.Equal(HttpStatusCode.BadRequest, ssrf.StatusCode);
        var problem = await ssrf.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal(ErrorCodes.NotificationsPushEndpointRejected, problem.GetProperty("code").GetString());

        for (var i = 0; i < PushSubscription.MaxPerUser; i++)
        {
            var ok = await client.PostAsJsonAsync(
                "/api/v1/me/push-subscriptions",
                new UpsertPushSubscriptionRequest(
                    $"https://fcm.googleapis.com/fcm/send/kadr-max-{i}",
                    new PushKeysDto($"p256-{i}", $"auth-{i}")));
            Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        }

        var eleventh = await client.PostAsJsonAsync(
            "/api/v1/me/push-subscriptions",
            new UpsertPushSubscriptionRequest(
                "https://fcm.googleapis.com/fcm/send/kadr-max-overflow",
                new PushKeysDto("p256-x", "auth-x")));
        Assert.Equal(HttpStatusCode.Conflict, eleventh.StatusCode);
    }

    [Fact]
    public async Task Delivery_runner_sends_localized_push_and_deletes_on_gone()
    {
        await factory.ResetDatabaseAsync();
        factory.Push.Clear();
        var helper = new AuthTestHelper(factory);
        var account = await helper.RegisterVerifiedAsync("Push Run Co", "push-run@example.com");
        var userId = await helper.WithTenantAsync(account.TenantId, db =>
            db.Users.AsNoTracking().Where(u => u.Email == account.Email.ToLowerInvariant()).Select(u => u.Id).SingleAsync());

        await helper.WithTenantAsync(account.TenantId, async db =>
        {
            db.PushSubscriptions.Add(PushSubscription.Create(
                account.TenantId,
                userId,
                FcmEndpoint,
                "p256dh-run",
                "auth-run",
                "Test",
                "UA"));
            var n = Notification.Create(
                account.TenantId,
                userId,
                NotificationEventTypes.WorkflowApprovalRequired,
                "موافقة مطلوبة",
                "Approval required",
                "لديك طلب بانتظار الموافقة",
                "You have a request awaiting approval",
                "/leave/approvals",
                null);
            db.Notifications.Add(n);
            var now = DateTimeOffset.UtcNow;
            db.NotificationDeliveries.Add(NotificationDelivery.Sent(account.TenantId, n.Id, NotificationChannel.InApp, now));
            db.NotificationDeliveries.Add(NotificationDelivery.Skipped(account.TenantId, n.Id, NotificationChannel.Email, now));
            db.NotificationDeliveries.Add(NotificationDelivery.Pending(account.TenantId, n.Id, NotificationChannel.Push, now));
            await db.SaveChangesAsync();
            return 0;
        });

        using (var scope = factory.Services.CreateScope())
        {
            var attempted = await scope.ServiceProvider.GetRequiredService<INotificationDeliveryRunner>().RunOnceAsync();
            Assert.True(attempted >= 1);
        }

        Assert.NotNull(factory.Push.LastMessage);
        Assert.Equal("موافقة مطلوبة", factory.Push.LastMessage!.Title);
        Assert.Equal("/leave/approvals", factory.Push.LastMessage.LinkPath);
        Assert.DoesNotContain("p256dh", factory.Push.LastMessage.Body, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("ar", factory.Push.LastMessage.Lang);

        factory.Push.Clear();
        factory.Push.NextOutcomes.Enqueue(PushSendOutcome.SubscriptionGone);
        await helper.WithTenantAsync(account.TenantId, async db =>
        {
            db.PushSubscriptions.Add(PushSubscription.Create(
                account.TenantId,
                userId,
                "https://fcm.googleapis.com/fcm/send/kadr-gone",
                "p256dh-gone",
                "auth-gone",
                null,
                null));
            var n = Notification.Create(
                account.TenantId,
                userId,
                NotificationEventTypes.WorkflowRequestApproved,
                "تمت الموافقة",
                "Approved",
                "تمت الموافقة على طلبك",
                "Your request was approved",
                "/leave/me",
                null);
            db.Notifications.Add(n);
            var now = DateTimeOffset.UtcNow;
            db.NotificationDeliveries.Add(NotificationDelivery.Pending(account.TenantId, n.Id, NotificationChannel.Push, now));
            await db.SaveChangesAsync();
            return 0;
        });

        using (var scope = factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<INotificationDeliveryRunner>().RunOnceAsync();
        }

        var remaining = await helper.WithTenantAsync(account.TenantId, db =>
            db.PushSubscriptions.AsNoTracking().CountAsync(s => s.Endpoint == "https://fcm.googleapis.com/fcm/send/kadr-gone"));
        Assert.Equal(0, remaining);
    }

    [Fact]
    public async Task Skips_inactive_subscription()
    {
        await factory.ResetDatabaseAsync();
        factory.Push.Clear();
        var helper = new AuthTestHelper(factory);
        var account = await helper.RegisterVerifiedAsync("Push Inactive Co", "push-inactive@example.com");
        var userId = await helper.WithTenantAsync(account.TenantId, db =>
            db.Users.AsNoTracking().Where(u => u.Email == account.Email.ToLowerInvariant()).Select(u => u.Id).SingleAsync());

        await helper.WithTenantAsync(account.TenantId, async db =>
        {
            var sub = PushSubscription.Create(
                account.TenantId,
                userId,
                FcmEndpoint,
                "p256dh",
                "auth",
                null,
                null);
            sub.SetActive(false);
            db.PushSubscriptions.Add(sub);
            var n = Notification.Create(
                account.TenantId,
                userId,
                NotificationEventTypes.WorkflowApprovalRequired,
                "عنوان",
                "Title",
                "نص",
                "Body",
                "/leave/approvals",
                null);
            db.Notifications.Add(n);
            db.NotificationDeliveries.Add(
                NotificationDelivery.Pending(account.TenantId, n.Id, NotificationChannel.Push, DateTimeOffset.UtcNow));
            await db.SaveChangesAsync();
            return 0;
        });

        using (var scope = factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<INotificationDeliveryRunner>().RunOnceAsync();
        }

        Assert.Empty(factory.Push.Sent);
    }

    [Fact]
    public async Task Send_test_uses_capturing_sender()
    {
        await factory.ResetDatabaseAsync();
        factory.Push.Clear();
        var helper = new AuthTestHelper(factory);
        var account = await helper.RegisterVerifiedAsync("Push Test Co", "push-test@example.com");
        var session = await helper.LoginSessionAsync(account.Email, AuthTestHelper.Password, account.Slug);
        var client = helper.Authed(session.AccessToken);

        var upsert = await client.PostAsJsonAsync(
            "/api/v1/me/push-subscriptions",
            new UpsertPushSubscriptionRequest(FcmEndpoint, new PushKeysDto("p256dh", "auth"), "Device"));
        Assert.Equal(HttpStatusCode.OK, upsert.StatusCode);

        var test = await client.PostAsync("/api/v1/me/push-subscriptions/test", null);
        Assert.Equal(HttpStatusCode.NoContent, test.StatusCode);
        Assert.NotNull(factory.Push.LastMessage);
        Assert.Equal("/notifications", factory.Push.LastMessage!.LinkPath);
    }

    [Fact]
    public async Task Upsert_enables_push_preferences_so_creator_queues_pending()
    {
        await factory.ResetDatabaseAsync();
        var helper = new AuthTestHelper(factory);
        var account = await helper.RegisterVerifiedAsync("Push Prefs Co", "push-prefs@example.com");
        var session = await helper.LoginSessionAsync(account.Email, AuthTestHelper.Password, account.Slug);
        var client = helper.Authed(session.AccessToken);
        var userId = await helper.WithTenantAsync(account.TenantId, db =>
            db.Users.AsNoTracking().Where(u => u.Email == account.Email.ToLowerInvariant()).Select(u => u.Id).SingleAsync());

        var upsert = await client.PostAsJsonAsync(
            "/api/v1/me/push-subscriptions",
            new UpsertPushSubscriptionRequest(FcmEndpoint, new PushKeysDto("p256dh", "auth"), "Device"));
        Assert.Equal(HttpStatusCode.OK, upsert.StatusCode);

        var prefs = await helper.WithTenantAsync(account.TenantId, db =>
            db.NotificationPreferences.AsNoTracking().Where(p => p.UserId == userId).ToListAsync());
        Assert.Equal(NotificationEventTypes.All.Count, prefs.Count);
        Assert.All(prefs, p => Assert.True(p.PushEnabled));

        await helper.WithTenantAsync(account.TenantId, async db =>
        {
            using var scope = factory.Services.CreateScope();
            var realtime = scope.ServiceProvider.GetRequiredService<INotificationRealtimeNotifier>();
            await NotificationCreator.CreateAsync(
                db,
                realtime,
                account.TenantId,
                userId,
                NotificationContentBuilder.ApprovalRequired(
                    WorkflowRequestType.Leave,
                    "أحمد",
                    "Ahmed"),
                "/leave/approvals",
                null,
                CancellationToken.None);
            return 0;
        });

        var pushDelivery = await helper.WithTenantAsync(account.TenantId, db =>
            db.NotificationDeliveries.AsNoTracking()
                .Where(d => d.Channel == NotificationChannel.Push)
                .OrderByDescending(d => d.CreatedAt)
                .FirstAsync());
        Assert.Equal(NotificationDeliveryStatus.Pending, pushDelivery.Status);
        Assert.Null(pushDelivery.LastError);
    }
}
