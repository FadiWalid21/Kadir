using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Kadr.Api.Endpoints;
using Kadr.Platform.Application.PlatformHealth;
using Kadr.TenantIsolation.Tests.Auth;
using Kadr.TenantIsolation.Tests.Register;

namespace Kadr.TenantIsolation.Tests.PlatformAdmin;

[Collection(RegisterCollection.Name)]
public sealed class PlatformHealthTests : IAsyncLifetime
{
    private const string Url = "/api/v1/platform/health";
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly RegisterApiFactory _factory;

    public PlatformHealthTests(RegisterApiFactory factory) => _factory = factory;

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Tenant_jwt_and_anonymous_cannot_read_platform_health()
    {
        var helper = new AuthTestHelper(_factory);
        var account = await helper.RegisterVerifiedAsync("Co A", "owner-a@example.com");
        var session = await helper.LoginSessionAsync(account.Email, AuthTestHelper.Password, account.Slug);

        Assert.Equal(HttpStatusCode.Forbidden, (await helper.Authed(session.AccessToken).GetAsync(Url)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _factory.CreateClient().GetAsync(Url)).StatusCode);
    }

    [Fact]
    public async Task Health_reports_real_database_state_and_honest_provider_state()
    {
        var client = await PlatformClientAsync();

        var response = await client.GetAsync(Url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var health = (await response.Content.ReadFromJsonAsync<PlatformHealthDto>(JsonOptions))!;
        var api = Component(health, PlatformHealthComponents.Api);
        var database = Component(health, PlatformHealthComponents.Database);
        var storage = Component(health, PlatformHealthComponents.Storage);
        var email = Component(health, PlatformHealthComponents.Email);
        var webPush = Component(health, PlatformHealthComponents.WebPush);

        Assert.Equal(PlatformHealthStatuses.Ok, api.Status);
        Assert.Equal(PlatformHealthStatuses.Ok, database.Status);
        Assert.NotNull(database.LatencyMs);
        Assert.Null(database.Detail);

        // The test host wires local disk storage (not durable) and a captured test sender (a real provider
        // that has no probe), so the aggregate is degraded rather than a blanket "ok".
        Assert.Equal(PlatformHealthStatuses.Degraded, storage.Status);
        Assert.Equal("local", storage.Provider);
        Assert.Equal(PlatformHealthStatuses.Configured, email.Status);
        Assert.Equal(PlatformHealthStatuses.Configured, webPush.Status);
        Assert.Equal(PlatformHealthStatuses.Degraded, health.Status);
        Assert.True(health.CheckedAt > DateTimeOffset.UtcNow.AddMinutes(-5));
    }

    private static PlatformHealthComponentDto Component(PlatformHealthDto health, string name) =>
        health.Components.Single(c => c.Name == name);

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
}
