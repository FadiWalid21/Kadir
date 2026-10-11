using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Kadr.Api.Endpoints;
using Kadr.Infrastructure.Persistence;
using Kadr.Platform.Application.Auth;
using Kadr.SharedKernel.Constants;
using Kadr.SharedKernel.Tenancy;
using Kadr.TenantIsolation.Tests.Register;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kadr.TenantIsolation.Tests.Auth;

/// <summary>Shared HTTP helpers for the Google, sign-in method, and password reset tests.</summary>
internal sealed class AuthTestHelper(RegisterApiFactory factory)
{
    public const string Password = "Str0ngPass!!";
    public const string NewPassword = "An0ther$trongPw";

    public static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public RegisterApiFactory Factory { get; } = factory;

    public async Task<TenantAccount> RegisterVerifiedAsync(string companyName, string email)
    {
        Factory.Emails.Clear();
        var client = Factory.CreateClient();
        var response = await RegisterAsync(client, companyName, email, Password, googleRegistrationToken: null);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var reg = (await response.Content.ReadFromJsonAsync<RegisterTenantResponse>(JsonOptions))!;
        var token = Factory.Emails.ExtractLastVerificationToken();
        Assert.NotNull(token);
        var verify = await client.PostAsJsonAsync("/api/v1/auth/verify-email", new VerifyEmailRequest(token));
        Assert.Equal(HttpStatusCode.NoContent, verify.StatusCode);
        return new TenantAccount(reg.Slug, email, reg.TenantId);
    }

    public async Task<TenantAccount> RegisterUnverifiedAsync(string companyName, string email)
    {
        var response = await RegisterAsync(
            Factory.CreateClient(), companyName, email, Password, googleRegistrationToken: null);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var reg = (await response.Content.ReadFromJsonAsync<RegisterTenantResponse>(JsonOptions))!;
        return new TenantAccount(reg.Slug, email, reg.TenantId);
    }

    public static async Task<HttpResponseMessage> RegisterAsync(
        HttpClient client,
        string companyName,
        string email,
        string? password,
        string? googleRegistrationToken,
        string? idempotencyKey = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/register")
        {
            Content = JsonContent.Create(new RegisterTenantRequest(
                "Owner",
                email,
                password,
                companyName,
                "office",
                "Africa/Cairo",
                "ar",
                "EG",
                GoogleRegistrationToken: googleRegistrationToken)),
        };
        request.Headers.TryAddWithoutValidation(
            HttpHeaderNames.IdempotencyKey,
            idempotencyKey ?? Guid.NewGuid().ToString("N"));
        return await client.SendAsync(request);
    }

    public static Task<HttpResponseMessage> LoginAsync(HttpClient client, string email, string password, string slug) =>
        client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, password, slug));

    public async Task<LoginSession> LoginSessionAsync(string email, string password, string slug)
    {
        var response = await LoginAsync(Factory.CreateClient(), email, password, slug);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ToSessionAsync(response);
    }

    public static async Task<LoginSession> ToSessionAsync(HttpResponseMessage response)
    {
        var body = (await response.Content.ReadFromJsonAsync<AccessTokenResponse>(JsonOptions))!;
        return new LoginSession(body.AccessToken, GetRefreshCookieValue(response));
    }

    public async Task<string> IssueNonceAsync()
    {
        var response = await Factory.CreateClient().PostAsync("/api/v1/auth/google/nonce", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<GoogleNonceResponse>(JsonOptions))!.Nonce;
    }

    public async Task<string> NewCredentialAsync(
        string subject,
        string email,
        string? name = "Google User",
        bool emailVerified = true) =>
        FakeGoogleIdTokenValidator.CreateCredential(await IssueNonceAsync(), subject, email, name, emailVerified);

    public async Task<HttpResponseMessage> GoogleLoginAsync(string subject, string email, bool emailVerified = true) =>
        await Factory.CreateClient().PostAsJsonAsync(
            "/api/v1/auth/google/login",
            new GoogleLoginRequest(await NewCredentialAsync(subject, email, emailVerified: emailVerified)));

    /// <summary>Signs in with Google when the email is unknown and returns the registration token from the 404.</summary>
    public async Task<string> GetRegistrationTokenAsync(string subject, string email)
    {
        var response = await GoogleLoginAsync(subject, email);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(ErrorCodes.AuthGoogleAccountNotFound, doc.RootElement.GetProperty("code").GetString());
        return doc.RootElement.GetProperty("registrationToken").GetString()!;
    }

    public HttpClient Authed(string accessToken)
    {
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }

    public static HttpRequestMessage WithRefreshCookie(HttpRequestMessage request, string refreshCookie)
    {
        request.Headers.TryAddWithoutValidation("Cookie", $"{AuthConstants.RefreshCookieName}={refreshCookie}");
        return request;
    }

    public async Task<HttpResponseMessage> RefreshAsync(string refreshCookie)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refresh");
        request.Headers.TryAddWithoutValidation(AuthConstants.CsrfHeaderName, AuthConstants.CsrfHeaderValue);
        WithRefreshCookie(request, refreshCookie);
        return await Factory.CreateClient().SendAsync(request);
    }

    public static async Task<string?> ReadErrorCodeAsync(HttpResponseMessage response)
    {
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.TryGetProperty("code", out var code) ? code.GetString() : null;
    }

    public static string GetRefreshCookieValue(HttpResponseMessage response)
    {
        var setCookie = response.Headers.GetValues("Set-Cookie")
            .First(v => v.StartsWith(AuthConstants.RefreshCookieName + "=", StringComparison.Ordinal));
        return setCookie.Split(';')[0][(AuthConstants.RefreshCookieName.Length + 1)..];
    }

    public async Task<T> WithTenantAsync<T>(Guid tenantId, Func<KadrDbContext, Task<T>> action)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KadrDbContext>();
        scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>().Set(tenantId);
        return await action(db);
    }

    public async Task SetUserActiveAsync(Guid tenantId, string email, bool isActive)
    {
        await WithTenantAsync(tenantId, async db =>
        {
            var user = await db.Users.SingleAsync(u => u.Email == email.ToLowerInvariant());
            user.SetActive(isActive);
            await db.SaveChangesAsync();
            return 0;
        });
    }

    public Task<int> CountAuditAsync(Guid tenantId, string action) =>
        WithTenantAsync(tenantId, db => db.AuditLogs.CountAsync(a => a.Action == action));

    /// <summary>Extracts every "Reset token: X" value from the last captured email.</summary>
    public IReadOnlyList<string> ExtractResetTokens(int emailIndex = -1)
    {
        var messages = Factory.Emails.Messages;
        var message = emailIndex < 0 ? messages[^1] : messages[emailIndex];
        return Regex.Matches(message.HtmlBody, @"Reset token: (\S+)")
            .Select(m => m.Groups[1].Value)
            .ToList();
    }
}

internal sealed record TenantAccount(string Slug, string Email, Guid TenantId);

internal sealed record LoginSession(string AccessToken, string RefreshCookie);
