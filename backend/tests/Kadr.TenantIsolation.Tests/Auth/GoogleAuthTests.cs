using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kadr.Api.Endpoints;
using Kadr.Identity.Application.Auth;
using Kadr.Identity.Application.Options;
using Kadr.Identity.Domain.Tokens;
using Kadr.Infrastructure.Persistence;
using Kadr.Platform.Application.Auth;
using Kadr.SharedKernel.Constants;
using Kadr.TenantIsolation.Tests.Register;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Kadr.TenantIsolation.Tests.Auth;

[Collection(RegisterCollection.Name)]
public sealed class GoogleAuthTests(RegisterApiFactory factory)
{
    private static readonly JsonSerializerOptions JsonOptions = AuthTestHelper.JsonOptions;
    private readonly AuthTestHelper _auth = new(factory);

    [Fact]
    public async Task Providers_reports_enabled_with_client_id()
    {
        await factory.ResetDatabaseAsync();

        var response = await factory.CreateClient().GetAsync("/api/v1/auth/providers");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<AuthProvidersResponse>(JsonOptions))!;
        Assert.True(body.Google.Enabled);
        Assert.Equal(RegisterApiFactory.TestGoogleClientId, body.Google.ClientId);
    }

    [Fact]
    public async Task Providers_off_reports_disabled_and_google_endpoints_return_404()
    {
        await factory.ResetDatabaseAsync();
        var options = factory.Services.GetRequiredService<IOptions<GoogleAuthOptions>>().Value;
        var original = options.ClientId;
        options.ClientId = string.Empty;
        try
        {
            var client = factory.CreateClient();

            var providers = await client.GetAsync("/api/v1/auth/providers");
            var body = (await providers.Content.ReadFromJsonAsync<AuthProvidersResponse>(JsonOptions))!;
            Assert.False(body.Google.Enabled);
            Assert.Null(body.Google.ClientId);

            Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync("/api/v1/auth/google/nonce", null)).StatusCode);
            var login = await client.PostAsJsonAsync(
                "/api/v1/auth/google/login",
                new GoogleLoginRequest("a.b.c"));
            Assert.Equal(HttpStatusCode.NotFound, login.StatusCode);
            var select = await client.PostAsJsonAsync(
                "/api/v1/auth/google/select-tenant",
                new GoogleSelectTenantRequest("x", Guid.NewGuid()));
            Assert.Equal(HttpStatusCode.NotFound, select.StatusCode);
        }
        finally
        {
            options.ClientId = original;
        }
    }

    [Fact]
    public async Task Existing_verified_email_signs_in_with_google_and_links_the_identity()
    {
        await factory.ResetDatabaseAsync();
        var account = await _auth.RegisterVerifiedAsync("Google Link Co", "g-link@example.com");

        var response = await _auth.GoogleLoginAsync("sub-link-1", account.Email);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var tokens = (await response.Content.ReadFromJsonAsync<AccessTokenResponse>(JsonOptions))!;
        Assert.False(string.IsNullOrWhiteSpace(tokens.AccessToken));
        Assert.Contains(AuthConstants.RefreshCookieName, response.Headers.GetValues("Set-Cookie").First());

        var linked = await _auth.WithTenantAsync(
            account.TenantId,
            db => db.ExternalLogins.CountAsync(l => l.ProviderSubject == "sub-link-1"));
        Assert.Equal(1, linked);
    }

    [Fact]
    public async Task Linked_subject_signs_in_even_when_google_email_changed()
    {
        await factory.ResetDatabaseAsync();
        var account = await _auth.RegisterVerifiedAsync("Google Sub Co", "g-sub@example.com");
        Assert.Equal(HttpStatusCode.OK, (await _auth.GoogleLoginAsync("sub-stable", account.Email)).StatusCode);

        var response = await _auth.GoogleLoginAsync("sub-stable", "renamed-mailbox@example.com");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Unknown_email_returns_not_found_with_registration_token()
    {
        await factory.ResetDatabaseAsync();

        var response = await _auth.GoogleLoginAsync("sub-new", "brand-new@example.com");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(ErrorCodes.AuthGoogleAccountNotFound, doc.RootElement.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(doc.RootElement.GetProperty("registrationToken").GetString()));
    }

    [Fact]
    public async Task Multiple_companies_require_selection_then_select_tenant_signs_in()
    {
        await factory.ResetDatabaseAsync();
        const string email = "g-multi@example.com";
        var a = await _auth.RegisterVerifiedAsync("Multi Alpha Co", email);
        var b = await _auth.RegisterVerifiedAsync("Multi Beta Co", email);

        var response = await _auth.GoogleLoginAsync("sub-multi", email);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var selection = (await response.Content.ReadFromJsonAsync<GoogleTenantSelectionDto>(JsonOptions))!;
        Assert.Equal(GoogleLoginStatuses.TenantSelectionRequired, selection.Status);
        Assert.Equal(2, selection.Companies.Count);
        Assert.Contains(selection.Companies, c => c.TenantId == a.TenantId);
        Assert.Contains(selection.Companies, c => c.TenantId == b.TenantId);

        var select = await factory.CreateClient().PostAsJsonAsync(
            "/api/v1/auth/google/select-tenant",
            new GoogleSelectTenantRequest(selection.SelectionToken, b.TenantId));
        Assert.Equal(HttpStatusCode.OK, select.StatusCode);
        Assert.Contains(AuthConstants.RefreshCookieName, select.Headers.GetValues("Set-Cookie").First());

        var reuse = await factory.CreateClient().PostAsJsonAsync(
            "/api/v1/auth/google/select-tenant",
            new GoogleSelectTenantRequest(selection.SelectionToken, b.TenantId));
        Assert.Equal(HttpStatusCode.Unauthorized, reuse.StatusCode);
        Assert.Equal(ErrorCodes.AuthGoogleSelectionInvalid, await AuthTestHelper.ReadErrorCodeAsync(reuse));
    }

    [Fact]
    public async Task Select_tenant_rejects_a_tenant_that_was_not_offered()
    {
        await factory.ResetDatabaseAsync();
        const string email = "g-offer@example.com";
        await _auth.RegisterVerifiedAsync("Offer Alpha Co", email);
        await _auth.RegisterVerifiedAsync("Offer Beta Co", email);
        var outsider = await _auth.RegisterVerifiedAsync("Offer Outsider Co", "outsider@example.com");

        var selection = (await (await _auth.GoogleLoginAsync("sub-offer", email))
            .Content.ReadFromJsonAsync<GoogleTenantSelectionDto>(JsonOptions))!;

        var select = await factory.CreateClient().PostAsJsonAsync(
            "/api/v1/auth/google/select-tenant",
            new GoogleSelectTenantRequest(selection.SelectionToken, outsider.TenantId));

        Assert.Equal(HttpStatusCode.Unauthorized, select.StatusCode);
    }

    [Fact]
    public async Task Deactivated_account_is_rejected()
    {
        await factory.ResetDatabaseAsync();
        var account = await _auth.RegisterVerifiedAsync("Google Off Co", "g-off@example.com");
        await _auth.SetUserActiveAsync(account.TenantId, account.Email, false);

        var response = await _auth.GoogleLoginAsync("sub-off", account.Email);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(ErrorCodes.AuthAccountDisabled, await AuthTestHelper.ReadErrorCodeAsync(response));
    }

    [Fact]
    public async Task Unconfirmed_account_cannot_sign_in_with_google()
    {
        await factory.ResetDatabaseAsync();
        var account = await _auth.RegisterUnverifiedAsync("Google Unverified Co", "g-unverified@example.com");

        var response = await _auth.GoogleLoginAsync("sub-unverified", account.Email);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(ErrorCodes.AuthEmailNotVerified, await AuthTestHelper.ReadErrorCodeAsync(response));
    }

    [Fact]
    public async Task Google_email_not_verified_is_rejected()
    {
        await factory.ResetDatabaseAsync();
        var account = await _auth.RegisterVerifiedAsync("Google Unv Mail Co", "g-unvmail@example.com");

        var response = await _auth.GoogleLoginAsync("sub-unvmail", account.Email, emailVerified: false);

        Assert.True(response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized);
        Assert.Equal(ErrorCodes.AuthGoogleEmailUnverified, await AuthTestHelper.ReadErrorCodeAsync(response));
    }

    [Fact]
    public async Task Nonce_is_single_use()
    {
        await factory.ResetDatabaseAsync();
        var account = await _auth.RegisterVerifiedAsync("Google Nonce Co", "g-nonce@example.com");
        var credential = await _auth.NewCredentialAsync("sub-nonce", account.Email);
        var client = factory.CreateClient();

        var first = await client.PostAsJsonAsync("/api/v1/auth/google/login", new GoogleLoginRequest(credential));
        var replay = await client.PostAsJsonAsync("/api/v1/auth/google/login", new GoogleLoginRequest(credential));

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);
        Assert.Equal(ErrorCodes.AuthGoogleNonceInvalid, await AuthTestHelper.ReadErrorCodeAsync(replay));
    }

    [Fact]
    public async Task Nonce_that_was_never_issued_is_rejected()
    {
        await factory.ResetDatabaseAsync();
        var credential = FakeGoogleIdTokenValidator.CreateCredential("never-issued", "sub-x", "x@example.com");

        var response = await factory.CreateClient().PostAsJsonAsync(
            "/api/v1/auth/google/login",
            new GoogleLoginRequest(credential));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(ErrorCodes.AuthGoogleNonceInvalid, await AuthTestHelper.ReadErrorCodeAsync(response));
    }

    [Fact]
    public async Task Expired_nonce_is_rejected()
    {
        await factory.ResetDatabaseAsync();
        var account = await _auth.RegisterVerifiedAsync("Google Expired Co", "g-expired@example.com");
        var credential = await _auth.NewCredentialAsync("sub-expired", account.Email);
        factory.Clock.Advance(AuthConstants.GoogleNonceLifetime.Add(TimeSpan.FromSeconds(1)));

        var response = await factory.CreateClient().PostAsJsonAsync(
            "/api/v1/auth/google/login",
            new GoogleLoginRequest(credential));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(ErrorCodes.AuthGoogleNonceInvalid, await AuthTestHelper.ReadErrorCodeAsync(response));
    }

    [Fact]
    public async Task Register_with_google_token_creates_confirmed_owner_without_password_or_verification_email()
    {
        await factory.ResetDatabaseAsync();
        factory.Emails.Clear();
        const string email = "g-register@example.com";
        var registrationToken = await _auth.GetRegistrationTokenAsync("sub-register", email);

        // Body email is ignored: identity comes from the token.
        var response = await AuthTestHelper.RegisterAsync(
            factory.CreateClient(),
            "Google Register Co",
            "attacker@example.com",
            password: null,
            registrationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var reg = (await response.Content.ReadFromJsonAsync<RegisterTenantResponse>(JsonOptions))!;
        Assert.Empty(factory.Emails.Messages);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KadrDbContext>();
        var user = await db.Users.IgnoreQueryFilters().SingleAsync(u => u.TenantId == reg.TenantId);
        Assert.Equal(email, user.Email);
        Assert.True(user.EmailConfirmed);
        Assert.False(user.HasPassword);
        Assert.Equal(1, await db.ExternalLogins.IgnoreQueryFilters()
            .CountAsync(l => l.TenantId == reg.TenantId && l.ProviderSubject == "sub-register"));
        Assert.Equal(0, await db.EmailVerificationTokens.IgnoreQueryFilters()
            .CountAsync(t => t.TenantId == reg.TenantId));
        Assert.Equal(1, await db.AuditLogs.IgnoreQueryFilters()
            .CountAsync(a => a.TenantId == reg.TenantId && a.Action == AuditActions.TenantRegisteredWithGoogle));

        // The new owner can immediately sign in with Google.
        var login = await _auth.GoogleLoginAsync("sub-register", email);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    [Fact]
    public async Task Register_with_google_token_is_single_use_and_rejects_unknown_tokens()
    {
        await factory.ResetDatabaseAsync();
        var token = await _auth.GetRegistrationTokenAsync("sub-once", "g-once@example.com");

        var first = await AuthTestHelper.RegisterAsync(factory.CreateClient(), "Once Co", "x@example.com", null, token);
        var second = await AuthTestHelper.RegisterAsync(factory.CreateClient(), "Twice Co", "x@example.com", null, token);
        var unknown = await AuthTestHelper.RegisterAsync(
            factory.CreateClient(), "Unknown Co", "x@example.com", null, "not-a-real-token");

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
        Assert.Equal(ErrorCodes.AuthGoogleRegistrationInvalid, await AuthTestHelper.ReadErrorCodeAsync(second));
        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
        Assert.Equal(ErrorCodes.AuthGoogleRegistrationInvalid, await AuthTestHelper.ReadErrorCodeAsync(unknown));
    }

    [Fact]
    public async Task Register_with_expired_google_token_is_rejected()
    {
        await factory.ResetDatabaseAsync();
        var token = await _auth.GetRegistrationTokenAsync("sub-exp-reg", "g-exp-reg@example.com");
        factory.Clock.Advance(AuthConstants.GoogleRegistrationLifetime.Add(TimeSpan.FromMinutes(1)));

        var response = await AuthTestHelper.RegisterAsync(
            factory.CreateClient(), "Expired Reg Co", "x@example.com", null, token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(ErrorCodes.AuthGoogleRegistrationInvalid, await AuthTestHelper.ReadErrorCodeAsync(response));
    }

    [Fact]
    public async Task Register_with_google_token_and_optional_password_validates_the_password()
    {
        await factory.ResetDatabaseAsync();
        var weakToken = await _auth.GetRegistrationTokenAsync("sub-opt-weak", "g-opt-weak@example.com");
        var weak = await AuthTestHelper.RegisterAsync(
            factory.CreateClient(), "Weak Opt Co", "x@example.com", "short", weakToken);
        Assert.Equal(HttpStatusCode.BadRequest, weak.StatusCode);
        Assert.Equal(ErrorCodes.AuthValidationFailed, await AuthTestHelper.ReadErrorCodeAsync(weak));

        // A rejected request must not burn the token.
        var okToken = await _auth.GetRegistrationTokenAsync("sub-opt-ok", "g-opt-ok@example.com");
        var ok = await AuthTestHelper.RegisterAsync(
            factory.CreateClient(), "Strong Opt Co", "x@example.com", AuthTestHelper.Password, okToken);
        Assert.Equal(HttpStatusCode.Created, ok.StatusCode);
        var reg = (await ok.Content.ReadFromJsonAsync<RegisterTenantResponse>(JsonOptions))!;

        var login = await AuthTestHelper.LoginAsync(
            factory.CreateClient(), "g-opt-ok@example.com", AuthTestHelper.Password, reg.Slug);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        // The weak attempt left its token usable.
        var retry = await AuthTestHelper.RegisterAsync(
            factory.CreateClient(), "Weak Retry Co", "x@example.com", AuthTestHelper.Password, weakToken);
        Assert.Equal(HttpStatusCode.Created, retry.StatusCode);
    }

    [Fact]
    public async Task Register_without_password_and_without_google_token_is_rejected()
    {
        await factory.ResetDatabaseAsync();

        var response = await AuthTestHelper.RegisterAsync(
            factory.CreateClient(), "No Pw Co", "nopw@example.com", password: null, googleRegistrationToken: null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(ErrorCodes.AuthValidationFailed, await AuthTestHelper.ReadErrorCodeAsync(response));
    }

    [Fact]
    public async Task Registration_token_cannot_be_used_as_a_nonce()
    {
        await factory.ResetDatabaseAsync();
        var token = await _auth.GetRegistrationTokenAsync("sub-purpose", "g-purpose@example.com");
        var credential = FakeGoogleIdTokenValidator.CreateCredential(token, "sub-purpose", "g-purpose@example.com");

        var response = await factory.CreateClient().PostAsJsonAsync(
            "/api/v1/auth/google/login",
            new GoogleLoginRequest(credential));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(ErrorCodes.AuthGoogleNonceInvalid, await AuthTestHelper.ReadErrorCodeAsync(response));

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KadrDbContext>();
        Assert.Equal(1, await db.AuthOneTimeTokens.CountAsync(
            t => t.Purpose == AuthOneTimeTokenPurpose.GoogleRegistration && t.ConsumedAt == null));
    }
}
