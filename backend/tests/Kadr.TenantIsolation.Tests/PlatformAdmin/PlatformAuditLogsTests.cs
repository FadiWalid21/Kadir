using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Kadr.Api.Endpoints;
using Kadr.Infrastructure.Persistence;
using Kadr.Platform.Application.PlatformAudit;
using Kadr.Platform.Application.PlatformTenants;
using Kadr.Platform.Domain.Audit;
using Kadr.SharedKernel.Constants;
using Kadr.TenantIsolation.Tests.Auth;
using Kadr.TenantIsolation.Tests.Register;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kadr.TenantIsolation.Tests.PlatformAdmin;

[Collection(RegisterCollection.Name)]
public sealed class PlatformAuditLogsTests : IAsyncLifetime
{
    private const string Url = "/api/v1/platform/audit-logs";
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly RegisterApiFactory _factory;

    public PlatformAuditLogsTests(RegisterApiFactory factory) => _factory = factory;

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Tenant_jwt_and_anonymous_cannot_read_the_global_audit_feed()
    {
        var helper = new AuthTestHelper(_factory);
        var account = await helper.RegisterVerifiedAsync("Co A", "owner-a@example.com");
        var session = await helper.LoginSessionAsync(account.Email, AuthTestHelper.Password, account.Slug);

        var tenantResponse = await helper.Authed(session.AccessToken).GetAsync(Url);
        Assert.Equal(HttpStatusCode.Forbidden, tenantResponse.StatusCode);

        var anonymous = await _factory.CreateClient().GetAsync(Url);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
    }

    [Fact]
    public async Task Feed_spans_tenants_and_carries_tenant_name_and_slug()
    {
        var helper = new AuthTestHelper(_factory);
        var a = await helper.RegisterVerifiedAsync("Alpha Trading", "owner-a@example.com");
        var b = await helper.RegisterVerifiedAsync("Beta Clinic", "owner-b@example.com");
        await SeedAsync(helper, a.TenantId, "custom.alpha", "Widget", userId: null);
        await SeedAsync(helper, b.TenantId, "custom.beta", "Gadget", userId: null);
        var client = await PlatformClientAsync();

        var page = await GetAsync(client, $"{Url}?pageSize=100");

        var alpha = page.Items.Single(i => i.Action == "custom.alpha");
        Assert.Equal(a.TenantId, alpha.TenantId);
        Assert.Equal("Alpha Trading", alpha.TenantName);
        Assert.Equal(a.Slug, alpha.TenantSlug);
        var beta = page.Items.Single(i => i.Action == "custom.beta");
        Assert.Equal("Beta Clinic", beta.TenantName);
        Assert.Equal(b.Slug, beta.TenantSlug);
        Assert.Equal(2, page.Items.Select(i => i.TenantId).Distinct().Count());
    }

    [Fact]
    public async Task Filters_by_tenant_user_entity_type_action_and_time_range()
    {
        var helper = new AuthTestHelper(_factory);
        var a = await helper.RegisterVerifiedAsync("Alpha Trading", "owner-a@example.com");
        var b = await helper.RegisterVerifiedAsync("Beta Clinic", "owner-b@example.com");
        var ownerA = await OwnerIdAsync(helper, a.TenantId);
        await SeedAsync(helper, a.TenantId, "custom.alpha", "Widget", ownerA);
        await SeedAsync(helper, b.TenantId, "custom.beta", "Gadget", userId: null);
        var client = await PlatformClientAsync();

        var byTenant = await GetAsync(client, $"{Url}?tenantId={b.TenantId}&pageSize=100");
        Assert.NotEmpty(byTenant.Items);
        Assert.All(byTenant.Items, i => Assert.Equal(b.TenantId, i.TenantId));

        var byUser = await GetAsync(client, $"{Url}?userId={ownerA}&pageSize=100");
        Assert.NotEmpty(byUser.Items);
        Assert.All(byUser.Items, i => Assert.Equal(ownerA, i.UserId));
        var actor = byUser.Items.Single(i => i.Action == "custom.alpha");
        Assert.Equal(PlatformAuditActorKinds.TenantUser, actor.ActorKind);
        Assert.Equal("owner-a@example.com", actor.ActorEmail);

        var byEntity = await GetAsync(client, $"{Url}?entityType=Gadget");
        Assert.Equal("custom.beta", Assert.Single(byEntity.Items).Action);

        var byAction = await GetAsync(client, $"{Url}?action=custom.alpha");
        Assert.Equal(a.TenantId, Assert.Single(byAction.Items).TenantId);

        var none = await GetAsync(client, $"{Url}?tenantId={a.TenantId}&action=custom.beta");
        Assert.Empty(none.Items);
        Assert.Null(none.NextCursor);

        var future = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(1).ToString("O"));
        var after = await GetAsync(client, $"{Url}?from={future}");
        Assert.Empty(after.Items);

        var past = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(-1).ToString("O"));
        var before = await GetAsync(client, $"{Url}?to={past}");
        Assert.Empty(before.Items);

        var windowFrom = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddMinutes(-10).ToString("O"));
        var windowTo = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddMinutes(10).ToString("O"));
        var window = await GetAsync(client, $"{Url}?action=custom.alpha&from={windowFrom}&to={windowTo}");
        Assert.Single(window.Items);
    }

    [Fact]
    public async Task Platform_admin_actions_are_attributed_to_the_admin()
    {
        var helper = new AuthTestHelper(_factory);
        var a = await helper.RegisterVerifiedAsync("Alpha Trading", "owner-a@example.com");
        var client = await PlatformClientAsync();

        var suspend = await client.PostAsJsonAsync(
            $"/api/v1/platform/tenants/{a.TenantId}/suspend",
            new PlatformReasonRequest("Unpaid invoice"));
        Assert.Equal(HttpStatusCode.OK, suspend.StatusCode);

        var page = await GetAsync(client, $"{Url}?action={PlatformAuditActions.TenantSuspended}");

        var item = Assert.Single(page.Items);
        Assert.Equal(a.TenantId, item.TenantId);
        Assert.Equal("Alpha Trading", item.TenantName);
        Assert.Equal(PlatformAuditActorKinds.PlatformAdmin, item.ActorKind);
        Assert.Equal(RegisterApiFactory.PlatformAdminEmail, item.ActorEmail);
        Assert.Equal("Test Operator", item.ActorName);
        Assert.Contains("Unpaid invoice", item.NewValuesJson);
    }

    [Fact]
    public async Task Cursor_pagination_walks_every_row_once_in_descending_order()
    {
        var helper = new AuthTestHelper(_factory);
        var a = await helper.RegisterVerifiedAsync("Alpha Trading", "owner-a@example.com");
        var b = await helper.RegisterVerifiedAsync("Beta Clinic", "owner-b@example.com");
        for (var i = 0; i < 4; i++)
        {
            await SeedAsync(helper, a.TenantId, $"custom.a{i}", "Widget", userId: null);
            await SeedAsync(helper, b.TenantId, $"custom.b{i}", "Gadget", userId: null);
        }

        int total;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KadrDbContext>();
            total = await db.AuditLogs.IgnoreQueryFilters().CountAsync();
        }

        Assert.True(total > 8);
        var client = await PlatformClientAsync();

        var seen = new List<PlatformAuditLogItemDto>();
        string? cursor = null;
        var pages = 0;
        do
        {
            var url = cursor is null
                ? $"{Url}?pageSize=3"
                : $"{Url}?pageSize=3&cursor={Uri.EscapeDataString(cursor)}";
            var page = await GetAsync(client, url);
            Assert.True(page.Items.Count <= 3);
            seen.AddRange(page.Items);
            cursor = page.NextCursor;
            pages++;
            Assert.True(pages < 50, "pagination did not terminate");
        }
        while (cursor is not null);

        Assert.Equal(total, seen.Count);
        Assert.Equal(total, seen.Select(i => i.Id).Distinct().Count());
        Assert.Equal(
            seen.OrderByDescending(i => i.At).ThenByDescending(i => i.Id).Select(i => i.Id),
            seen.Select(i => i.Id));
        Assert.True(pages > 1);
    }

    [Fact]
    public async Task Invalid_cursor_or_filters_return_problem_details()
    {
        var client = await PlatformClientAsync();

        var badCursor = await client.GetAsync($"{Url}?cursor=not-a-cursor");
        Assert.Equal(HttpStatusCode.BadRequest, badCursor.StatusCode);
        Assert.Equal(ErrorCodes.AuditCursorInvalid, await AuthTestHelper.ReadErrorCodeAsync(badCursor));

        var from = Uri.EscapeDataString(DateTimeOffset.UtcNow.ToString("O"));
        var to = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(-1).ToString("O"));
        var reversed = await client.GetAsync($"{Url}?from={from}&to={to}");
        Assert.Equal(HttpStatusCode.BadRequest, reversed.StatusCode);
        Assert.Equal(ErrorCodes.PlatformValidationFailed, await AuthTestHelper.ReadErrorCodeAsync(reversed));

        var negative = await client.GetAsync($"{Url}?pageSize=-1");
        Assert.Equal(HttpStatusCode.BadRequest, negative.StatusCode);

        var tooLong = await client.GetAsync($"{Url}?action={new string('x', 101)}");
        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
    }

    private static async Task SeedAsync(
        AuthTestHelper helper,
        Guid tenantId,
        string action,
        string entityType,
        Guid? userId) =>
        await helper.WithTenantAsync(tenantId, async db =>
        {
            db.AuditLogs.Add(AuditLog.Create(tenantId, userId, action, entityType, Guid.NewGuid(), null, "{}"));
            await db.SaveChangesAsync();
            return 0;
        });

    private static Task<Guid> OwnerIdAsync(AuthTestHelper helper, Guid tenantId) =>
        helper.WithTenantAsync(tenantId, db => db.Users.Select(u => u.Id).SingleAsync());

    private async Task<HttpClient> PlatformClientAsync()
    {
        var client = _factory.CreateCookieClient();
        var login = await client.PostAsJsonAsync(
            "/api/v1/platform/auth/login",
            new PlatformLoginRequest(RegisterApiFactory.PlatformAdminEmail, RegisterApiFactory.PlatformAdminPassword));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var tokens = (await login.Content.ReadFromJsonAsync<AccessTokenResponse>(JsonOptions))!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        return client;
    }

    private static async Task<PlatformAuditLogListResultDto> GetAsync(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<PlatformAuditLogListResultDto>(JsonOptions))!;
    }
}
