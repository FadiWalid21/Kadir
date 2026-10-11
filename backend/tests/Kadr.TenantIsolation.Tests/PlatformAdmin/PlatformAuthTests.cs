using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Kadr.Api.Endpoints;
using Kadr.SharedKernel.Constants;
using Kadr.TenantIsolation.Tests.Auth;
using Kadr.TenantIsolation.Tests.Register;

namespace Kadr.TenantIsolation.Tests.PlatformAdmin;

[Collection(RegisterCollection.Name)]
public sealed class PlatformAuthTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly RegisterApiFactory _factory;

    public PlatformAuthTests(RegisterApiFactory factory) => _factory = factory;

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Platform_login_succeeds_for_seeded_admin()
    {
        var client = _factory.CreateCookieClient();
        var response = await client.PostAsJsonAsync(
            "/api/v1/platform/auth/login",
            new PlatformLoginRequest(RegisterApiFactory.PlatformAdminEmail, RegisterApiFactory.PlatformAdminPassword));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AccessTokenResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(body.AccessToken));
        Assert.Contains(response.Headers.GetValues("Set-Cookie"), c => c.Contains("kadr_platform_refresh"));
    }

    [Fact]
    public async Task Platform_login_rejects_bad_password()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            "/api/v1/platform/auth/login",
            new PlatformLoginRequest(RegisterApiFactory.PlatformAdminEmail, "wrong-password!!"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Tenant_jwt_is_forbidden_on_platform_me()
    {
        var helper = new AuthTestHelper(_factory);
        var account = await helper.RegisterVerifiedAsync("Co A", "owner-a@example.com");
        var session = await helper.LoginSessionAsync(account.Email, AuthTestHelper.Password, account.Slug);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        var response = await client.GetAsync("/api/v1/platform/me");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Platform_jwt_can_call_platform_me()
    {
        var client = _factory.CreateCookieClient();
        var login = await client.PostAsJsonAsync(
            "/api/v1/platform/auth/login",
            new PlatformLoginRequest(RegisterApiFactory.PlatformAdminEmail, RegisterApiFactory.PlatformAdminPassword));
        var tokens = await login.Content.ReadFromJsonAsync<AccessTokenResponse>(JsonOptions);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);

        var response = await client.GetAsync("/api/v1/platform/me");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Platform_refresh_rotates_access_token()
    {
        var client = _factory.CreateCookieClient();
        var login = await client.PostAsJsonAsync(
            "/api/v1/platform/auth/login",
            new PlatformLoginRequest(RegisterApiFactory.PlatformAdminEmail, RegisterApiFactory.PlatformAdminPassword));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        var refresh = new HttpRequestMessage(HttpMethod.Post, "/api/v1/platform/auth/refresh");
        refresh.Headers.TryAddWithoutValidation(AuthConstants.CsrfHeaderName, AuthConstants.CsrfHeaderValue);
        var response = await client.SendAsync(refresh);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AccessTokenResponse>(JsonOptions);
        Assert.NotNull(body?.AccessToken);
    }
}
