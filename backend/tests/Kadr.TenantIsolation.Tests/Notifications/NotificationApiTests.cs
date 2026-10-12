using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kadr.Platform.Application.Abstractions;
using Kadr.Platform.Application.Home;
using Kadr.Platform.Application.Notifications;
using Kadr.Platform.Domain.Notifications;
using Kadr.SharedKernel.Tenancy;
using Kadr.TenantIsolation.Tests.Auth;
using Kadr.TenantIsolation.Tests.Register;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kadr.TenantIsolation.Tests.Notifications;

[Collection(RegisterCollection.Name)]
public sealed class NotificationApiTests(RegisterApiFactory factory)
{
    private static readonly JsonSerializerOptions JsonOptions = AuthTestHelper.JsonOptions;

    [Fact]
    public async Task Notifications_are_tenant_and_recipient_isolated()
    {
        await factory.ResetDatabaseAsync();
        var helper = new AuthTestHelper(factory);
        var a = await helper.RegisterVerifiedAsync("Notif Co A", "notif-a@example.com");
        var b = await helper.RegisterVerifiedAsync("Notif Co B", "notif-b@example.com");
        var sessionA = await helper.LoginSessionAsync(a.Email, AuthTestHelper.Password, a.Slug);
        var sessionB = await helper.LoginSessionAsync(b.Email, AuthTestHelper.Password, b.Slug);
        var clientA = helper.Authed(sessionA.AccessToken);
        var clientB = helper.Authed(sessionB.AccessToken);

        var userA = await helper.WithTenantAsync(a.TenantId, db =>
            db.Users.AsNoTracking().Where(u => u.Email == a.Email.ToLowerInvariant()).Select(u => u.Id).SingleAsync());
        var userB = await helper.WithTenantAsync(b.TenantId, db =>
            db.Users.AsNoTracking().Where(u => u.Email == b.Email.ToLowerInvariant()).Select(u => u.Id).SingleAsync());

        await SeedNotificationAsync(a.TenantId, userA, "A title", "A body");
        await SeedNotificationAsync(b.TenantId, userB, "B title", "B body");

        var listA = await clientA.GetFromJsonAsync<PagedNotificationsDto>("/api/v1/notifications?page=1&pageSize=20", JsonOptions);
        Assert.NotNull(listA);
        Assert.Equal(1, listA.TotalCount);
        Assert.Equal(1, listA.UnreadCount);
        Assert.Equal("A title", listA.Items[0].TitleEn);

        var listB = await clientB.GetFromJsonAsync<PagedNotificationsDto>("/api/v1/notifications", JsonOptions);
        Assert.NotNull(listB);
        Assert.Equal(1, listB.TotalCount);
        Assert.Equal("B title", listB.Items[0].TitleEn);

        var cross = await clientB.PostAsync($"/api/v1/notifications/{listA.Items[0].Id}/read", null);
        Assert.Equal(HttpStatusCode.NotFound, cross.StatusCode);
    }

    [Fact]
    public async Task Unauthenticated_notifications_are_forbidden()
    {
        await factory.ResetDatabaseAsync();
        var client = factory.CreateClient();
        var response = await client.GetAsync("/api/v1/notifications");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Mark_read_and_read_all_update_unread_count()
    {
        await factory.ResetDatabaseAsync();
        var helper = new AuthTestHelper(factory);
        var account = await helper.RegisterVerifiedAsync("Notif Read Co", "notif-read@example.com");
        var session = await helper.LoginSessionAsync(account.Email, AuthTestHelper.Password, account.Slug);
        var client = helper.Authed(session.AccessToken);
        var userId = await helper.WithTenantAsync(account.TenantId, db =>
            db.Users.AsNoTracking().Where(u => u.Email == account.Email.ToLowerInvariant()).Select(u => u.Id).SingleAsync());

        var id1 = await SeedNotificationAsync(account.TenantId, userId, "One", "Body 1");
        await SeedNotificationAsync(account.TenantId, userId, "Two", "Body 2");

        var unread = await client.GetFromJsonAsync<UnreadCountDto>("/api/v1/notifications/unread-count", JsonOptions);
        Assert.Equal(2, unread!.Count);

        var marked = await client.PostAsync($"/api/v1/notifications/{id1}/read", null);
        Assert.Equal(HttpStatusCode.OK, marked.StatusCode);
        unread = await client.GetFromJsonAsync<UnreadCountDto>("/api/v1/notifications/unread-count", JsonOptions);
        Assert.Equal(1, unread!.Count);

        var all = await client.PostAsync("/api/v1/notifications/read-all", null);
        Assert.Equal(HttpStatusCode.OK, all.StatusCode);
        unread = await client.GetFromJsonAsync<UnreadCountDto>("/api/v1/notifications/unread-count", JsonOptions);
        Assert.Equal(0, unread!.Count);
    }

    [Fact]
    public async Task Preferences_defaults_and_update_round_trip()
    {
        await factory.ResetDatabaseAsync();
        var helper = new AuthTestHelper(factory);
        var account = await helper.RegisterVerifiedAsync("Notif Pref Co", "notif-pref@example.com");
        var session = await helper.LoginSessionAsync(account.Email, AuthTestHelper.Password, account.Slug);
        var client = helper.Authed(session.AccessToken);

        var prefs = await client.GetFromJsonAsync<NotificationPreferencesDto>(
            "/api/v1/notifications/preferences",
            JsonOptions);
        Assert.NotNull(prefs);
        Assert.Equal(3, prefs.Items.Count);
        Assert.All(prefs.Items, i => Assert.True(i.InAppEnabled));
        Assert.All(prefs.Items, i => Assert.True(i.EmailEnabled));
        Assert.All(prefs.Items, i => Assert.False(i.PushEnabled));

        var update = await client.PutAsJsonAsync(
            "/api/v1/notifications/preferences",
            new UpdateNotificationPreferencesRequest(
            [
                new NotificationPreferenceDto(NotificationEventTypes.WorkflowApprovalRequired, true, false, true),
                new NotificationPreferenceDto(NotificationEventTypes.WorkflowRequestApproved, true, true, false),
                new NotificationPreferenceDto(NotificationEventTypes.WorkflowRequestRejected, false, false, false),
            ]));
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var updated = (await update.Content.ReadFromJsonAsync<NotificationPreferencesDto>(JsonOptions))!;
        Assert.False(updated.Items.Single(i => i.EventType == NotificationEventTypes.WorkflowApprovalRequired).EmailEnabled);
        Assert.True(updated.Items.Single(i => i.EventType == NotificationEventTypes.WorkflowApprovalRequired).PushEnabled);
    }

    [Fact]
    public async Task Email_delivery_runner_sends_pending_email()
    {
        await factory.ResetDatabaseAsync();
        var helper = new AuthTestHelper(factory);
        var account = await helper.RegisterVerifiedAsync("Notif Mail Co", "notif-mail@example.com");
        var userId = await helper.WithTenantAsync(account.TenantId, db =>
            db.Users.AsNoTracking().Where(u => u.Email == account.Email.ToLowerInvariant()).Select(u => u.Id).SingleAsync());

        factory.Emails.Clear();
        await SeedNotificationAsync(account.TenantId, userId, "Mail title", "Mail body", enqueueEmail: true);
        using (var scope = factory.Services.CreateScope())
        {
            var attempted = await scope.ServiceProvider.GetRequiredService<INotificationDeliveryRunner>().RunOnceAsync();
            Assert.True(attempted >= 1);
        }

        Assert.Contains(
            factory.Emails.Messages,
            m => m.To == account.Email && m.Subject.Contains("عنوان", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Home_dashboard_returns_greeting_and_hr_for_owner()
    {
        await factory.ResetDatabaseAsync();
        var helper = new AuthTestHelper(factory);
        var account = await helper.RegisterVerifiedAsync("Home Co", "home-dash@example.com");
        var session = await helper.LoginSessionAsync(account.Email, AuthTestHelper.Password, account.Slug);
        var client = helper.Authed(session.AccessToken);

        var response = await client.GetAsync("/api/v1/home/dashboard");
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, body);
        var dto = JsonSerializer.Deserialize<HomeDashboardDto>(body, JsonOptions);
        Assert.NotNull(dto);
        Assert.False(string.IsNullOrWhiteSpace(dto.Greeting.TenantName));
        Assert.NotNull(dto.Hr);
    }

    [Fact]
    public async Task Home_dashboard_uses_no_tracking_reads()
    {
        // Guards N+1 / accidental tracking: dashboard Selects to DTOs with AsNoTracking.
        await factory.ResetDatabaseAsync();
        var helper = new AuthTestHelper(factory);
        var account = await helper.RegisterVerifiedAsync("Home Qc Co", "home-qc@example.com");
        var userId = await helper.WithTenantAsync(account.TenantId, db =>
            db.Users.AsNoTracking().Where(u => u.Email == account.Email.ToLowerInvariant()).Select(u => u.Id).SingleAsync());

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Kadr.Infrastructure.Persistence.KadrDbContext>();
        scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>().Set(account.TenantId, userId);
        var result = await scope.ServiceProvider.GetRequiredService<IHomeDashboardService>().GetDashboardAsync();
        Assert.True(result.IsSuccess);
        Assert.Empty(db.ChangeTracker.Entries());
    }

    private Task<Guid> SeedNotificationAsync(
        Guid tenantId,
        Guid userId,
        string titleEn,
        string bodyEn,
        bool enqueueEmail = false) =>
        new AuthTestHelper(factory).WithTenantAsync(tenantId, async db =>
        {
            var n = Notification.Create(
                tenantId,
                userId,
                NotificationEventTypes.WorkflowApprovalRequired,
                "عنوان",
                titleEn,
                "نص",
                bodyEn,
                "/leave/approvals",
                null);
            db.Notifications.Add(n);
            var now = DateTimeOffset.UtcNow;
            db.NotificationDeliveries.Add(NotificationDelivery.Sent(tenantId, n.Id, NotificationChannel.InApp, now));
            db.NotificationDeliveries.Add(
                enqueueEmail
                    ? NotificationDelivery.Pending(tenantId, n.Id, NotificationChannel.Email, now)
                    : NotificationDelivery.Skipped(tenantId, n.Id, NotificationChannel.Email, now));
            db.NotificationDeliveries.Add(
                NotificationDelivery.Skipped(tenantId, n.Id, NotificationChannel.Push, now, "preference_off"));
            await db.SaveChangesAsync();
            return n.Id;
        });
}
