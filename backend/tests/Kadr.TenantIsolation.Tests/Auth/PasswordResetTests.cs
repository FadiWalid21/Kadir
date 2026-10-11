using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kadr.Platform.Application.Auth;
using Kadr.SharedKernel.Constants;
using Kadr.TenantIsolation.Tests.Register;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Kadr.TenantIsolation.Tests.Auth;

[Collection(RegisterCollection.Name)]
public sealed class PasswordResetTests(RegisterApiFactory factory)
{
    private readonly AuthTestHelper _auth = new(factory);

    [Fact]
    public async Task Forgot_password_returns_identical_accepted_response_for_known_and_unknown_emails()
    {
        await factory.ResetDatabaseAsync();
        var account = await _auth.RegisterVerifiedAsync("Forgot Co", "forgot@example.com");
        factory.Emails.Clear();

        var known = await ForgotAsync(account.Email);
        var knownBody = await known.Content.ReadAsStringAsync();
        Assert.Single(factory.Emails.Messages);
        Assert.Equal(1, await _auth.CountAuditAsync(account.TenantId, AuditActions.AuthPasswordResetRequested));

        factory.Emails.Clear();
        var unknown = await ForgotAsync("nobody@example.com");
        var unknownBody = await unknown.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Accepted, known.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, unknown.StatusCode);
        Assert.Equal(knownBody, unknownBody);
        using var doc = JsonDocument.Parse(unknownBody);
        Assert.Equal(ForgotPasswordStatuses.Accepted, doc.RootElement.GetProperty("status").GetString());
        Assert.Empty(factory.Emails.Messages);
        Assert.Equal(1, await _auth.CountAuditAsync(account.TenantId, AuditActions.AuthPasswordResetRequested));
    }

    [Fact]
    public async Task Forgot_password_ignores_blank_and_malformed_emails_without_error()
    {
        await factory.ResetDatabaseAsync();

        Assert.Equal(HttpStatusCode.Accepted, (await ForgotAsync("")).StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, (await ForgotAsync("   ")).StatusCode);
        Assert.Empty(factory.Emails.Messages);
    }

    [Fact]
    public async Task Reset_password_sets_new_password_revokes_sessions_and_token_is_single_use()
    {
        await factory.ResetDatabaseAsync();
        var account = await _auth.RegisterVerifiedAsync("Reset Co", "reset@example.com");
        var session = await _auth.LoginSessionAsync(account.Email, AuthTestHelper.Password, account.Slug);

        factory.Emails.Clear();
        await ForgotAsync(account.Email);
        var token = Assert.Single(_auth.ExtractResetTokens());

        factory.Emails.Clear();
        var reset = await ResetAsync(token, AuthTestHelper.NewPassword);

        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);
        Assert.Equal(1, await _auth.CountAuditAsync(account.TenantId, AuditActions.AuthPasswordResetCompleted));
        Assert.Single(factory.Emails.Messages);
        Assert.Equal(account.Email, factory.Emails.Last!.To);

        // No auto-login: the reset response carries no cookie or token.
        Assert.False(reset.Headers.Contains("Set-Cookie"));

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await AuthTestHelper.LoginAsync(factory.CreateClient(), account.Email, AuthTestHelper.Password, account.Slug)).StatusCode);
        Assert.Equal(
            HttpStatusCode.OK,
            (await AuthTestHelper.LoginAsync(factory.CreateClient(), account.Email, AuthTestHelper.NewPassword, account.Slug)).StatusCode);

        // Existing sessions were signed out.
        Assert.Equal(HttpStatusCode.Unauthorized, (await _auth.RefreshAsync(session.RefreshCookie)).StatusCode);

        var reuse = await ResetAsync(token, "Y3t.Another-Pass");
        Assert.Equal(HttpStatusCode.BadRequest, reuse.StatusCode);
        Assert.Equal(ErrorCodes.AuthPasswordResetInvalid, await AuthTestHelper.ReadErrorCodeAsync(reuse));
    }

    [Fact]
    public async Task Reset_password_clears_a_lockout()
    {
        await factory.ResetDatabaseAsync();
        var account = await _auth.RegisterVerifiedAsync("Reset Lock Co", "reset-lock@example.com");
        for (var i = 0; i < 5; i++)
        {
            await AuthTestHelper.LoginAsync(factory.CreateClient(), account.Email, "WrongPass!!x", account.Slug);
        }

        factory.Emails.Clear();
        await ForgotAsync(account.Email);
        var token = Assert.Single(_auth.ExtractResetTokens());
        Assert.Equal(HttpStatusCode.NoContent, (await ResetAsync(token, AuthTestHelper.NewPassword)).StatusCode);

        Assert.Equal(
            HttpStatusCode.OK,
            (await AuthTestHelper.LoginAsync(factory.CreateClient(), account.Email, AuthTestHelper.NewPassword, account.Slug)).StatusCode);
    }

    [Fact]
    public async Task Unknown_empty_and_tampered_tokens_are_rejected_with_the_same_code()
    {
        await factory.ResetDatabaseAsync();
        var account = await _auth.RegisterVerifiedAsync("Tamper Co", "tamper@example.com");
        factory.Emails.Clear();
        await ForgotAsync(account.Email);
        var token = Assert.Single(_auth.ExtractResetTokens());

        foreach (var bad in new[] { "", "not-a-token", token + "x", token[..^2] + "AA" })
        {
            var response = await ResetAsync(bad, AuthTestHelper.NewPassword);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(ErrorCodes.AuthPasswordResetInvalid, await AuthTestHelper.ReadErrorCodeAsync(response));
        }

        Assert.Equal(HttpStatusCode.NoContent, (await ResetAsync(token, AuthTestHelper.NewPassword)).StatusCode);
    }

    [Fact]
    public async Task Expired_token_is_rejected()
    {
        await factory.ResetDatabaseAsync();
        var account = await _auth.RegisterVerifiedAsync("Expire Co", "expire@example.com");
        factory.Emails.Clear();
        await ForgotAsync(account.Email);
        var token = Assert.Single(_auth.ExtractResetTokens());

        factory.Clock.Advance(AuthConstants.PasswordResetLifetime.Add(TimeSpan.FromSeconds(1)));

        var response = await ResetAsync(token, AuthTestHelper.NewPassword);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(ErrorCodes.AuthPasswordResetInvalid, await AuthTestHelper.ReadErrorCodeAsync(response));
    }

    [Fact]
    public async Task Weak_password_is_rejected_without_burning_the_token()
    {
        await factory.ResetDatabaseAsync();
        var account = await _auth.RegisterVerifiedAsync("Weak Reset Co", "weak-reset@example.com");
        factory.Emails.Clear();
        await ForgotAsync(account.Email);
        var token = Assert.Single(_auth.ExtractResetTokens());

        var tooShort = await ResetAsync(token, "short");
        Assert.Equal(HttpStatusCode.BadRequest, tooShort.StatusCode);
        Assert.Equal(ErrorCodes.AuthPasswordTooShort, await AuthTestHelper.ReadErrorCodeAsync(tooShort));

        var common = await ResetAsync(token, "1234567890");
        Assert.Equal(ErrorCodes.AuthPasswordTooCommon, await AuthTestHelper.ReadErrorCodeAsync(common));

        Assert.Equal(HttpStatusCode.NoContent, (await ResetAsync(token, AuthTestHelper.NewPassword)).StatusCode);
    }

    [Fact]
    public async Task A_newer_request_invalidates_the_previous_token()
    {
        await factory.ResetDatabaseAsync();
        var account = await _auth.RegisterVerifiedAsync("Newer Co", "newer@example.com");
        factory.Emails.Clear();
        await ForgotAsync(account.Email);
        var first = Assert.Single(_auth.ExtractResetTokens());
        factory.Emails.Clear();
        await ForgotAsync(account.Email);
        var second = Assert.Single(_auth.ExtractResetTokens());

        Assert.NotEqual(first, second);
        Assert.Equal(HttpStatusCode.BadRequest, (await ResetAsync(first, AuthTestHelper.NewPassword)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await ResetAsync(second, AuthTestHelper.NewPassword)).StatusCode);
    }

    [Fact]
    public async Task Reset_confirms_an_unverified_email()
    {
        await factory.ResetDatabaseAsync();
        var account = await _auth.RegisterUnverifiedAsync("Unverified Reset Co", "unverified-reset@example.com");
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await AuthTestHelper.LoginAsync(factory.CreateClient(), account.Email, AuthTestHelper.Password, account.Slug)).StatusCode);

        factory.Emails.Clear();
        await ForgotAsync(account.Email);
        var token = Assert.Single(_auth.ExtractResetTokens());
        Assert.Equal(HttpStatusCode.NoContent, (await ResetAsync(token, AuthTestHelper.NewPassword)).StatusCode);

        Assert.Equal(
            HttpStatusCode.OK,
            (await AuthTestHelper.LoginAsync(factory.CreateClient(), account.Email, AuthTestHelper.NewPassword, account.Slug)).StatusCode);
    }

    [Fact]
    public async Task Google_only_account_without_a_password_can_set_one_by_reset()
    {
        await factory.ResetDatabaseAsync();
        const string email = "google-reset@example.com";
        var registrationToken = await _auth.GetRegistrationTokenAsync("sub-reset", email);
        var reg = await AuthTestHelper.RegisterAsync(
            factory.CreateClient(), "Google Reset Co", "ignored@example.com", null, registrationToken);
        Assert.Equal(HttpStatusCode.Created, reg.StatusCode);
        var slug = (await reg.Content.ReadFromJsonAsync<Kadr.Api.Endpoints.RegisterTenantResponse>(AuthTestHelper.JsonOptions))!.Slug;

        factory.Emails.Clear();
        await ForgotAsync(email);
        var token = Assert.Single(_auth.ExtractResetTokens());
        Assert.Equal(HttpStatusCode.NoContent, (await ResetAsync(token, AuthTestHelper.NewPassword)).StatusCode);

        Assert.Equal(
            HttpStatusCode.OK,
            (await AuthTestHelper.LoginAsync(factory.CreateClient(), email, AuthTestHelper.NewPassword, slug)).StatusCode);
    }

    [Fact]
    public async Task Same_email_in_two_companies_gets_one_email_with_a_labelled_token_per_account()
    {
        await factory.ResetDatabaseAsync();
        const string email = "multi-reset@example.com";
        var a = await _auth.RegisterVerifiedAsync("Reset Alpha Co", email);
        var b = await _auth.RegisterVerifiedAsync("Reset Beta Co", email);

        factory.Emails.Clear();
        await ForgotAsync(email);

        Assert.Single(factory.Emails.Messages);
        var body = factory.Emails.Last!.HtmlBody;
        Assert.Contains("Reset Alpha Co", body);
        Assert.Contains("Reset Beta Co", body);
        var tokens = _auth.ExtractResetTokens();
        Assert.Equal(2, tokens.Count);
        Assert.Equal(1, await _auth.CountAuditAsync(a.TenantId, AuditActions.AuthPasswordResetRequested));
        Assert.Equal(1, await _auth.CountAuditAsync(b.TenantId, AuditActions.AuthPasswordResetRequested));

        // Alpha is listed first (ordered by company name), so the first token resets Alpha only.
        Assert.True(body.IndexOf("Reset Alpha Co", StringComparison.Ordinal)
                    < body.IndexOf("Reset Beta Co", StringComparison.Ordinal));
        Assert.Equal(HttpStatusCode.NoContent, (await ResetAsync(tokens[0], AuthTestHelper.NewPassword)).StatusCode);

        Assert.Equal(
            HttpStatusCode.OK,
            (await AuthTestHelper.LoginAsync(factory.CreateClient(), email, AuthTestHelper.NewPassword, a.Slug)).StatusCode);
        Assert.Equal(
            HttpStatusCode.OK,
            (await AuthTestHelper.LoginAsync(factory.CreateClient(), email, AuthTestHelper.Password, b.Slug)).StatusCode);
    }

    [Fact]
    public async Task Deactivated_account_gets_no_reset_email()
    {
        await factory.ResetDatabaseAsync();
        var account = await _auth.RegisterVerifiedAsync("Inactive Reset Co", "inactive-reset@example.com");
        await _auth.SetUserActiveAsync(account.TenantId, account.Email, false);
        factory.Emails.Clear();

        var response = await ForgotAsync(account.Email);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Empty(factory.Emails.Messages);
        Assert.Equal(0, await _auth.CountAuditAsync(account.TenantId, AuditActions.AuthPasswordResetRequested));
    }

    [Fact]
    public async Task Per_email_throttle_silently_stops_sending_after_the_limit()
    {
        await factory.ResetDatabaseAsync();
        var account = await _auth.RegisterVerifiedAsync("Throttle Co", "throttle@example.com");
        factory.Emails.Clear();

        using var limited = factory.WithWebHostBuilder(builder =>
            builder.UseSetting("PasswordReset:EmailPermitLimit", "2"));
        var client = limited.CreateClient();

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 4; i++)
        {
            var response = await client.PostAsJsonAsync(
                "/api/v1/auth/forgot-password",
                new ForgotPasswordRequest(account.Email));
            statuses.Add(response.StatusCode);
        }

        Assert.All(statuses, s => Assert.Equal(HttpStatusCode.Accepted, s));
        Assert.Equal(2, factory.Emails.Messages.Count);
    }

    [Theory]
    [InlineData("/api/v1/auth/forgot-password", "auth-forgot")]
    [InlineData("/api/v1/auth/reset-password", "auth-reset")]
    public void Endpoints_use_their_rate_limit_policy(string route, string policy)
    {
        _ = factory.CreateClient();
        var source = factory.Services.GetRequiredService<EndpointDataSource>();
        var endpoint = source.Endpoints.OfType<RouteEndpoint>().Single(e => e.RoutePattern.RawText == route);
        var metadata = endpoint.Metadata.GetMetadata<EnableRateLimitingAttribute>();
        Assert.NotNull(metadata);
        Assert.Equal(policy, metadata.PolicyName);
    }

    [Fact]
    public void Reset_paths_are_allowed_while_the_subscription_is_read_only()
    {
        Assert.True(SubscriptionWriteAllowList.IsAllowed("/api/v1/auth/forgot-password"));
        Assert.True(SubscriptionWriteAllowList.IsAllowed("/api/v1/auth/reset-password"));
        Assert.True(SubscriptionWriteAllowList.IsAllowed("/api/v1/me/password"));
        Assert.True(SubscriptionWriteAllowList.IsAllowed("/api/v1/me/google"));
        Assert.True(SubscriptionWriteAllowList.IsAllowed("/api/v1/me/google/link"));
    }

    private Task<HttpResponseMessage> ForgotAsync(string email) =>
        factory.CreateClient().PostAsJsonAsync("/api/v1/auth/forgot-password", new ForgotPasswordRequest(email));

    private Task<HttpResponseMessage> ResetAsync(string token, string newPassword) =>
        factory.CreateClient().PostAsJsonAsync(
            "/api/v1/auth/reset-password",
            new ResetPasswordRequest(token, newPassword));
}
