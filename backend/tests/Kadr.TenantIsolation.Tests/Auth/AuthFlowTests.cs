using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Kadr.Api.Endpoints;
using Kadr.Identity.Application.Auth;
using Kadr.Identity.Domain.Roles;
using Kadr.Infrastructure.Persistence;
using Kadr.SharedKernel.Constants;
using Kadr.SharedKernel.Tenancy;
using Kadr.TenantIsolation.Tests.Register;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Kadr.TenantIsolation.Tests.Auth;

[Collection(RegisterCollection.Name)]
public sealed class AuthFlowTests(RegisterApiFactory factory)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private const string Password = "Str0ngPass!!";

    [Fact]
    public async Task Login_success_returns_access_token_claims_and_sets_refresh_cookie_same_error_for_bad_creds()
    {
        await factory.ResetDatabaseAsync();
        var client = factory.CreateCookieClient();
        var (slug, email, _) = await RegisterVerifiedAsync(client, "Login Co", "login-ok@example.com");

        var ok = await LoginAsync(client, email, Password, slug);
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        var body = await ok.Content.ReadFromJsonAsync<AccessTokenResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(body.AccessToken));
        Assert.Contains(AuthConstants.RefreshCookieName, ok.Headers.GetValues("Set-Cookie").First());

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(body.AccessToken);
        Assert.Contains(jwt.Claims, c => c.Type is ClaimNames.Subject or "sub");
        Assert.Contains(jwt.Claims, c => c.Type == ClaimNames.TenantId);
        Assert.Contains(jwt.Claims, c => c.Type == ClaimNames.Roles && c.Value == SystemRoleNames.Owner);
        Assert.Contains(jwt.Claims, c => c.Type == ClaimNames.Language);

        var wrongPassword = await LoginAsync(client, email, "WrongPass!!x", slug);
        var unknownEmail = await LoginAsync(client, "missing@example.com", Password, slug);
        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknownEmail.StatusCode);
        var wrongCode = await ReadErrorCodeAsync(wrongPassword);
        var unknownCode = await ReadErrorCodeAsync(unknownEmail);
        Assert.Equal(ErrorCodes.AuthInvalidCredentials, wrongCode);
        Assert.Equal(ErrorCodes.AuthInvalidCredentials, unknownCode);
    }

    [Fact]
    public async Task Unverified_cannot_login_until_verify_email()
    {
        await factory.ResetDatabaseAsync();
        var client = factory.CreateCookieClient();
        var registered = await RegisterAsync(client, "Verify Co", "verify@example.com");
        Assert.Equal(HttpStatusCode.Created, registered.StatusCode);
        var reg = await registered.Content.ReadFromJsonAsync<RegisterTenantResponse>(JsonOptions);
        Assert.NotNull(reg);

        var blocked = await LoginAsync(client, "verify@example.com", Password, reg.Slug);
        Assert.Equal(HttpStatusCode.Forbidden, blocked.StatusCode);
        Assert.Equal(ErrorCodes.AuthEmailNotVerified, await ReadErrorCodeAsync(blocked));

        var token = factory.Emails.ExtractLastVerificationToken();
        Assert.NotNull(token);
        var verify = await client.PostAsJsonAsync("/api/v1/auth/verify-email", new VerifyEmailRequest(token));
        Assert.Equal(HttpStatusCode.NoContent, verify.StatusCode);

        var ok = await LoginAsync(client, "verify@example.com", Password, reg.Slug);
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
    }

    [Fact]
    public async Task Five_failed_logins_lock_account_for_fifteen_minutes()
    {
        await factory.ResetDatabaseAsync();
        var client = factory.CreateCookieClient();
        var (slug, email, _) = await RegisterVerifiedAsync(client, "Lock Co", "lock@example.com");

        for (var i = 0; i < 5; i++)
        {
            var fail = await LoginAsync(client, email, "WrongPass!!x", slug);
            Assert.True(
                fail.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden,
                fail.StatusCode.ToString());
        }

        var locked = await LoginAsync(client, email, Password, slug);
        Assert.Equal(HttpStatusCode.Forbidden, locked.StatusCode);
        Assert.Equal(ErrorCodes.AuthAccountLocked, await ReadErrorCodeAsync(locked));

        factory.Clock.Advance(TimeSpan.FromMinutes(15).Add(TimeSpan.FromSeconds(1)));
        var unlocked = await LoginAsync(client, email, Password, slug);
        Assert.Equal(HttpStatusCode.OK, unlocked.StatusCode);
    }

    [Fact]
    public async Task Refresh_rotates_cookie_and_reuse_revokes_family()
    {
        await factory.ResetDatabaseAsync();
        var client = factory.CreateCookieClient();
        var (slug, email, _) = await RegisterVerifiedAsync(client, "Refresh Co", "refresh@example.com");

        var login = await LoginAsync(client, email, Password, slug);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var oldCookie = GetRefreshCookieValue(login);

        var refresh = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refresh");
        refresh.Headers.TryAddWithoutValidation(AuthConstants.CsrfHeaderName, AuthConstants.CsrfHeaderValue);
        var refreshResponse = await client.SendAsync(refresh);
        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);
        var newCookie = GetRefreshCookieValue(refreshResponse);
        Assert.NotEqual(oldCookie, newCookie);

        var reuseClient = factory.CreateCookieClient();
        reuseClient.DefaultRequestHeaders.TryAddWithoutValidation(AuthConstants.CsrfHeaderName, AuthConstants.CsrfHeaderValue);
        var reuse = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refresh");
        reuse.Headers.TryAddWithoutValidation(AuthConstants.CsrfHeaderName, AuthConstants.CsrfHeaderValue);
        reuse.Headers.TryAddWithoutValidation("Cookie", $"{AuthConstants.RefreshCookieName}={oldCookie}");
        var reused = await reuseClient.SendAsync(reuse);
        Assert.Equal(HttpStatusCode.Unauthorized, reused.StatusCode);
        Assert.Equal(ErrorCodes.AuthRefreshReuse, await ReadErrorCodeAsync(reused));

        var secondRefresh = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refresh");
        secondRefresh.Headers.TryAddWithoutValidation(AuthConstants.CsrfHeaderName, AuthConstants.CsrfHeaderValue);
        var familyDead = await client.SendAsync(secondRefresh);
        Assert.Equal(HttpStatusCode.Unauthorized, familyDead.StatusCode);
    }

    [Fact]
    public async Task Permission_check_endpoint_enforces_users_invite()
    {
        await factory.ResetDatabaseAsync();
        var client = factory.CreateCookieClient();
        var (slug, email, tenantId) = await RegisterVerifiedAsync(client, "Perm Co", "perm@example.com");

        var login = await LoginAsync(client, email, Password, slug);
        var tokens = await login.Content.ReadFromJsonAsync<AccessTokenResponse>(JsonOptions);
        Assert.NotNull(tokens);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        var ownerOk = await client.GetAsync("/api/v1/dev/permission-check");
        Assert.Equal(HttpStatusCode.OK, ownerOk.StatusCode);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KadrDbContext>();
            var ambient = scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();
            ambient.Set(tenantId);
            var user = await db.Users.SingleAsync();
            var employeeRole = await db.Roles.SingleAsync(r => r.Name == SystemRoleNames.Employee);
            var ownerLink = await db.UserRoles.SingleAsync(ur => ur.UserId == user.Id);
            db.UserRoles.Remove(ownerLink);
            db.UserRoles.Add(UserRole.Create(tenantId, user.Id, employeeRole.Id));
            await db.SaveChangesAsync();
            scope.ServiceProvider.GetRequiredService<IPermissionCacheInvalidator>()
                .Invalidate(tenantId, user.Id);
        }

        var empLogin = await LoginAsync(factory.CreateCookieClient(), email, Password, slug);
        var empTokens = await empLogin.Content.ReadFromJsonAsync<AccessTokenResponse>(JsonOptions);
        var empClient = factory.CreateCookieClient();
        empClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", empTokens!.AccessToken);
        var forbidden = await empClient.GetAsync("/api/v1/dev/permission-check");
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }

    [Fact]
    public async Task Same_email_two_tenants_login_separately_and_client_tenantId_ignored()
    {
        await factory.ResetDatabaseAsync();
        var client = factory.CreateCookieClient();
        const string email = "shared-auth@example.com";
        var a = await RegisterVerifiedAsync(client, "Tenant A Auth", email, companySuffix: "a");
        var b = await RegisterVerifiedAsync(client, "Tenant B Auth", email, companySuffix: "b");

        var loginA = await LoginAsync(client, email, Password, a.Slug);
        var loginB = await LoginAsync(client, email, Password, b.Slug);
        Assert.Equal(HttpStatusCode.OK, loginA.StatusCode);
        Assert.Equal(HttpStatusCode.OK, loginB.StatusCode);

        var tokenA = (await loginA.Content.ReadFromJsonAsync<AccessTokenResponse>(JsonOptions))!.AccessToken;
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(tokenA);
        var tenantFromJwt = Guid.Parse(jwt.Claims.First(c => c.Type == ClaimNames.TenantId).Value);
        Assert.Equal(a.TenantId, tenantFromJwt);

        var meClient = factory.CreateCookieClient();
        meClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenA);
        meClient.DefaultRequestHeaders.TryAddWithoutValidation("X-Tenant-Id", b.TenantId.ToString());
        var me = await meClient.GetAsync($"/api/v1/me?tenantId={b.TenantId}");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        var meBody = await me.Content.ReadFromJsonAsync<MeResponse>(JsonOptions);
        Assert.Equal(a.TenantId, meBody!.Tenant.Id);
    }

    [Theory]
    [InlineData("ar", "البريد أو كلمة المرور غير صحيحة.")]
    [InlineData("en", "Email or password is incorrect.")]
    public void Localizer_auth_invalid_credentials(string lang, string expected)
    {
        var localizer = new Kadr.Api.Localization.ErrorLocalizer();
        Assert.Equal(expected, localizer.GetDetail(ErrorCodes.AuthInvalidCredentials, lang));
    }

    [Fact]
    public void Login_endpoint_requires_rate_limiting_policy()
    {
        _ = factory.CreateClient();
        var source = factory.Services.GetRequiredService<Microsoft.AspNetCore.Routing.EndpointDataSource>();
        var endpoint = source.Endpoints
            .OfType<Microsoft.AspNetCore.Routing.RouteEndpoint>()
            .Single(e => e.RoutePattern.RawText == "/api/v1/auth/login");
        var metadata = endpoint.Metadata.GetMetadata<Microsoft.AspNetCore.RateLimiting.EnableRateLimitingAttribute>();
        Assert.NotNull(metadata);
        Assert.Equal("auth-login", metadata.PolicyName);
    }

    private async Task<(string Slug, string Email, Guid TenantId)> RegisterVerifiedAsync(
        HttpClient client,
        string companyName,
        string email,
        string companySuffix = "")
    {
        factory.Emails.Clear();
        var response = await RegisterAsync(client, companyName + companySuffix, email);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var reg = await response.Content.ReadFromJsonAsync<RegisterTenantResponse>(JsonOptions);
        Assert.NotNull(reg);
        var token = factory.Emails.ExtractLastVerificationToken();
        Assert.NotNull(token);
        var verify = await client.PostAsJsonAsync("/api/v1/auth/verify-email", new VerifyEmailRequest(token));
        Assert.Equal(HttpStatusCode.NoContent, verify.StatusCode);
        return (reg.Slug, email, reg.TenantId);
    }

    private static async Task<HttpResponseMessage> RegisterAsync(HttpClient client, string companyName, string email)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/register")
        {
            Content = JsonContent.Create(new RegisterTenantRequest(
                "Owner",
                email,
                Password,
                companyName,
                "office",
                "Africa/Cairo",
                "ar",
                "EG")),
        };
        request.Headers.TryAddWithoutValidation(HttpHeaderNames.IdempotencyKey, Guid.NewGuid().ToString("N"));
        return await client.SendAsync(request);
    }

    private static Task<HttpResponseMessage> LoginAsync(
        HttpClient client,
        string email,
        string password,
        string slug) =>
        client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, password, slug));

    private static async Task<string?> ReadErrorCodeAsync(HttpResponseMessage response)
    {
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.TryGetProperty("code", out var code) ? code.GetString()
            : doc.RootElement.TryGetProperty("extensions", out var ext) && ext.TryGetProperty("code", out var nested)
                ? nested.GetString()
                : null;
    }

    private static string GetRefreshCookieValue(HttpResponseMessage response)
    {
        var setCookie = response.Headers.GetValues("Set-Cookie")
            .First(v => v.StartsWith(AuthConstants.RefreshCookieName + "=", StringComparison.Ordinal));
        var part = setCookie.Split(';')[0];
        return part[(AuthConstants.RefreshCookieName.Length + 1)..];
    }
}
