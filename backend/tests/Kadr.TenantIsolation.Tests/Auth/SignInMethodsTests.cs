using System.Net;
using System.Net.Http.Json;
using Kadr.Identity.Application.Options;
using Kadr.Platform.Application.Auth;
using Kadr.SharedKernel.Constants;
using Kadr.TenantIsolation.Tests.Register;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Kadr.TenantIsolation.Tests.Auth;

[Collection(RegisterCollection.Name)]
public sealed class SignInMethodsTests(RegisterApiFactory factory)
{
    private static readonly System.Text.Json.JsonSerializerOptions JsonOptions = AuthTestHelper.JsonOptions;
    private readonly AuthTestHelper _auth = new(factory);

    [Fact]
    public async Task Endpoints_require_authentication()
    {
        await factory.ResetDatabaseAsync();
        var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/me/sign-in-methods")).StatusCode);
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await client.PostAsJsonAsync("/api/v1/me/password", new SetPasswordRequest(null, "x"))).StatusCode);
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await client.PostAsJsonAsync("/api/v1/me/google/link", new LinkGoogleRequest("x"))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.DeleteAsync("/api/v1/me/google")).StatusCode);
    }

    [Fact]
    public async Task Get_reports_password_enabled_and_google_not_linked_for_password_account()
    {
        await factory.ResetDatabaseAsync();
        var account = await _auth.RegisterVerifiedAsync("Methods Co", "methods@example.com");
        var session = await _auth.LoginSessionAsync(account.Email, AuthTestHelper.Password, account.Slug);

        var response = await _auth.Authed(session.AccessToken).GetAsync("/api/v1/me/sign-in-methods");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<SignInMethodsResponse>(JsonOptions))!;
        Assert.True(body.Password.Enabled);
        Assert.False(body.Google.Linked);
        Assert.Null(body.Google.Email);
    }

    [Fact]
    public async Task Link_google_succeeds_audits_notifies_and_blocks_a_second_link()
    {
        await factory.ResetDatabaseAsync();
        var account = await _auth.RegisterVerifiedAsync("Link Co", "link@example.com");
        var session = await _auth.LoginSessionAsync(account.Email, AuthTestHelper.Password, account.Slug);
        var client = _auth.Authed(session.AccessToken);
        factory.Emails.Clear();

        var link = await client.PostAsJsonAsync(
            "/api/v1/me/google/link",
            new LinkGoogleRequest(await _auth.NewCredentialAsync("sub-linked", "my.google@gmail.com")));

        Assert.Equal(HttpStatusCode.NoContent, link.StatusCode);
        Assert.Equal(1, await _auth.CountAuditAsync(account.TenantId, AuditActions.AuthGoogleLinked));
        Assert.Single(factory.Emails.Messages);
        Assert.Equal(account.Email, factory.Emails.Last!.To);

        var methods = (await (await client.GetAsync("/api/v1/me/sign-in-methods"))
            .Content.ReadFromJsonAsync<SignInMethodsResponse>(JsonOptions))!;
        Assert.True(methods.Google.Linked);
        Assert.Equal("my.google@gmail.com", methods.Google.Email);

        // Same user linking again (even a different Google account) is rejected.
        var again = await client.PostAsJsonAsync(
            "/api/v1/me/google/link",
            new LinkGoogleRequest(await _auth.NewCredentialAsync("sub-other", "other@gmail.com")));
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal(ErrorCodes.AuthGoogleAlreadyLinked, await AuthTestHelper.ReadErrorCodeAsync(again));

        // The linked identity now signs in.
        Assert.Equal(HttpStatusCode.OK, (await _auth.GoogleLoginAsync("sub-linked", "my.google@gmail.com")).StatusCode);
    }

    [Fact]
    public async Task Link_google_rejects_a_subject_already_linked_to_another_user_in_the_tenant()
    {
        await factory.ResetDatabaseAsync();
        var account = await _auth.RegisterVerifiedAsync("Shared Sub Co", "owner-shared@example.com");
        var session = await _auth.LoginSessionAsync(account.Email, AuthTestHelper.Password, account.Slug);
        var client = _auth.Authed(session.AccessToken);
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await client.PostAsJsonAsync(
                "/api/v1/me/google/link",
                new LinkGoogleRequest(await _auth.NewCredentialAsync("sub-shared", "shared@gmail.com")))).StatusCode);

        // A second user in the same tenant tries to link the same Google subject.
        await _auth.WithTenantAsync(account.TenantId, async db =>
        {
            var owner = await db.Users.SingleAsync();
            var second = Kadr.Identity.Domain.Users.AppUser.CreateInvited(
                account.TenantId,
                "second@example.com",
                "SECOND@EXAMPLE.COM",
                "unused-hash",
                "Second User",
                "ar");
            db.Users.Add(second);
            await db.SaveChangesAsync();
            return owner.Id;
        });

        var secondToken = await LoginWithHashAsync(account, "second@example.com");
        var response = await _auth.Authed(secondToken).PostAsJsonAsync(
            "/api/v1/me/google/link",
            new LinkGoogleRequest(await _auth.NewCredentialAsync("sub-shared", "shared@gmail.com")));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(ErrorCodes.AuthGoogleAlreadyLinked, await AuthTestHelper.ReadErrorCodeAsync(response));
    }

    [Fact]
    public async Task Link_google_is_not_found_when_google_is_off()
    {
        await factory.ResetDatabaseAsync();
        var account = await _auth.RegisterVerifiedAsync("Link Off Co", "link-off@example.com");
        var session = await _auth.LoginSessionAsync(account.Email, AuthTestHelper.Password, account.Slug);
        var options = factory.Services.GetRequiredService<IOptions<GoogleAuthOptions>>().Value;
        var original = options.ClientId;
        options.ClientId = string.Empty;
        try
        {
            var response = await _auth.Authed(session.AccessToken).PostAsJsonAsync(
                "/api/v1/me/google/link",
                new LinkGoogleRequest("a.b.c"));
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
        finally
        {
            options.ClientId = original;
        }
    }

    [Fact]
    public async Task Link_google_rejects_a_replayed_credential()
    {
        await factory.ResetDatabaseAsync();
        var account = await _auth.RegisterVerifiedAsync("Link Replay Co", "link-replay@example.com");
        var session = await _auth.LoginSessionAsync(account.Email, AuthTestHelper.Password, account.Slug);
        var client = _auth.Authed(session.AccessToken);
        var credential = await _auth.NewCredentialAsync("sub-replay", "replay@gmail.com");

        Assert.Equal(
            HttpStatusCode.NoContent,
            (await client.PostAsJsonAsync("/api/v1/me/google/link", new LinkGoogleRequest(credential))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync("/api/v1/me/google")).StatusCode);

        var replay = await client.PostAsJsonAsync("/api/v1/me/google/link", new LinkGoogleRequest(credential));
        Assert.Equal(HttpStatusCode.BadRequest, replay.StatusCode);
        Assert.Equal(ErrorCodes.AuthGoogleNonceInvalid, await AuthTestHelper.ReadErrorCodeAsync(replay));
    }

    [Fact]
    public async Task Unlink_google_requires_a_password_then_succeeds_after_one_is_added()
    {
        await factory.ResetDatabaseAsync();
        const string email = "google-only@example.com";
        var registrationToken = await _auth.GetRegistrationTokenAsync("sub-only", email);
        var reg = await AuthTestHelper.RegisterAsync(
            factory.CreateClient(), "Google Only Co", "ignored@example.com", null, registrationToken);
        Assert.Equal(HttpStatusCode.Created, reg.StatusCode);

        var login = await _auth.GoogleLoginAsync("sub-only", email);
        var session = await AuthTestHelper.ToSessionAsync(login);
        var client = _auth.Authed(session.AccessToken);

        var methods = (await (await client.GetAsync("/api/v1/me/sign-in-methods"))
            .Content.ReadFromJsonAsync<SignInMethodsResponse>(JsonOptions))!;
        Assert.False(methods.Password.Enabled);
        Assert.True(methods.Google.Linked);

        var blocked = await client.DeleteAsync("/api/v1/me/google");
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
        Assert.Equal(ErrorCodes.AuthLastSignInMethod, await AuthTestHelper.ReadErrorCodeAsync(blocked));

        // Adding a password needs no current password because none exists.
        var add = await client.PostAsJsonAsync(
            "/api/v1/me/password",
            new SetPasswordRequest(null, AuthTestHelper.NewPassword));
        Assert.Equal(HttpStatusCode.NoContent, add.StatusCode);

        var tenantId = (await reg.Content.ReadFromJsonAsync<Kadr.Api.Endpoints.RegisterTenantResponse>(JsonOptions))!.TenantId;
        Assert.Equal(1, await _auth.CountAuditAsync(tenantId, AuditActions.AuthPasswordAdded));

        var unlink = await client.DeleteAsync("/api/v1/me/google");
        Assert.Equal(HttpStatusCode.NoContent, unlink.StatusCode);
        Assert.Equal(1, await _auth.CountAuditAsync(tenantId, AuditActions.AuthGoogleUnlinked));

        // Google no longer signs this user in (the email is unknown to Google lookup but matches the account).
        var after = (await (await client.GetAsync("/api/v1/me/sign-in-methods"))
            .Content.ReadFromJsonAsync<SignInMethodsResponse>(JsonOptions))!;
        Assert.True(after.Password.Enabled);
        Assert.False(after.Google.Linked);
    }

    [Fact]
    public async Task Set_password_requires_current_password_when_one_exists()
    {
        await factory.ResetDatabaseAsync();
        var account = await _auth.RegisterVerifiedAsync("Pw Change Co", "pw-change@example.com");
        var session = await _auth.LoginSessionAsync(account.Email, AuthTestHelper.Password, account.Slug);
        var client = _auth.Authed(session.AccessToken);

        var missing = await client.PostAsJsonAsync(
            "/api/v1/me/password",
            new SetPasswordRequest(null, AuthTestHelper.NewPassword));
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        Assert.Equal(ErrorCodes.AuthInvalidCredentials, await AuthTestHelper.ReadErrorCodeAsync(missing));

        var wrong = await client.PostAsJsonAsync(
            "/api/v1/me/password",
            new SetPasswordRequest("WrongCurrent!!1", AuthTestHelper.NewPassword));
        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);

        var weak = await client.PostAsJsonAsync(
            "/api/v1/me/password",
            new SetPasswordRequest(AuthTestHelper.Password, "short"));
        Assert.Equal(HttpStatusCode.BadRequest, weak.StatusCode);
        Assert.Equal(ErrorCodes.AuthPasswordTooShort, await AuthTestHelper.ReadErrorCodeAsync(weak));

        var common = await client.PostAsJsonAsync(
            "/api/v1/me/password",
            new SetPasswordRequest(AuthTestHelper.Password, "1234567890"));
        Assert.Equal(HttpStatusCode.BadRequest, common.StatusCode);
        Assert.Equal(ErrorCodes.AuthPasswordTooCommon, await AuthTestHelper.ReadErrorCodeAsync(common));

        factory.Emails.Clear();
        var ok = await client.PostAsJsonAsync(
            "/api/v1/me/password",
            new SetPasswordRequest(AuthTestHelper.Password, AuthTestHelper.NewPassword));
        Assert.Equal(HttpStatusCode.NoContent, ok.StatusCode);
        Assert.Equal(1, await _auth.CountAuditAsync(account.TenantId, AuditActions.AuthPasswordChanged));
        Assert.Single(factory.Emails.Messages);

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await AuthTestHelper.LoginAsync(factory.CreateClient(), account.Email, AuthTestHelper.Password, account.Slug)).StatusCode);
        Assert.Equal(
            HttpStatusCode.OK,
            (await AuthTestHelper.LoginAsync(factory.CreateClient(), account.Email, AuthTestHelper.NewPassword, account.Slug)).StatusCode);
    }

    [Fact]
    public async Task Password_change_keeps_the_callers_session_and_revokes_the_others()
    {
        await factory.ResetDatabaseAsync();
        var account = await _auth.RegisterVerifiedAsync("Sessions Co", "sessions@example.com");
        var current = await _auth.LoginSessionAsync(account.Email, AuthTestHelper.Password, account.Slug);
        var other = await _auth.LoginSessionAsync(account.Email, AuthTestHelper.Password, account.Slug);

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/me/password")
        {
            Content = JsonContent.Create(new SetPasswordRequest(AuthTestHelper.Password, AuthTestHelper.NewPassword)),
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", current.AccessToken);
        AuthTestHelper.WithRefreshCookie(request, current.RefreshCookie);
        var response = await factory.CreateClient().SendAsync(request);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await _auth.RefreshAsync(other.RefreshCookie)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _auth.RefreshAsync(current.RefreshCookie)).StatusCode);
    }

    [Fact]
    public async Task Password_change_without_a_refresh_cookie_revokes_every_session()
    {
        await factory.ResetDatabaseAsync();
        var account = await _auth.RegisterVerifiedAsync("No Cookie Co", "no-cookie@example.com");
        var first = await _auth.LoginSessionAsync(account.Email, AuthTestHelper.Password, account.Slug);
        var second = await _auth.LoginSessionAsync(account.Email, AuthTestHelper.Password, account.Slug);

        var response = await _auth.Authed(first.AccessToken).PostAsJsonAsync(
            "/api/v1/me/password",
            new SetPasswordRequest(AuthTestHelper.Password, AuthTestHelper.NewPassword));
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await _auth.RefreshAsync(first.RefreshCookie)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _auth.RefreshAsync(second.RefreshCookie)).StatusCode);
    }

    [Fact]
    public async Task Refresh_cookie_of_another_user_is_not_trusted_for_session_exemption()
    {
        await factory.ResetDatabaseAsync();
        var a = await _auth.RegisterVerifiedAsync("Cookie A Co", "cookie-a@example.com");
        var b = await _auth.RegisterVerifiedAsync("Cookie B Co", "cookie-b@example.com");
        var sessionA = await _auth.LoginSessionAsync(a.Email, AuthTestHelper.Password, a.Slug);
        var sessionB = await _auth.LoginSessionAsync(b.Email, AuthTestHelper.Password, b.Slug);

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/me/password")
        {
            Content = JsonContent.Create(new SetPasswordRequest(AuthTestHelper.Password, AuthTestHelper.NewPassword)),
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", sessionA.AccessToken);
        AuthTestHelper.WithRefreshCookie(request, sessionB.RefreshCookie);
        Assert.Equal(HttpStatusCode.NoContent, (await factory.CreateClient().SendAsync(request)).StatusCode);

        // A's own session is revoked (the foreign cookie did not exempt it) and B is untouched.
        Assert.Equal(HttpStatusCode.Unauthorized, (await _auth.RefreshAsync(sessionA.RefreshCookie)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _auth.RefreshAsync(sessionB.RefreshCookie)).StatusCode);
    }

    [Fact]
    public async Task Sign_in_methods_only_describe_the_callers_own_tenant_user()
    {
        await factory.ResetDatabaseAsync();
        var a = await _auth.RegisterVerifiedAsync("Iso A Co", "iso@example.com");
        var b = await _auth.RegisterVerifiedAsync("Iso B Co", "iso@example.com");
        var sessionA = await _auth.LoginSessionAsync(a.Email, AuthTestHelper.Password, a.Slug);
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await _auth.Authed(sessionA.AccessToken).PostAsJsonAsync(
                "/api/v1/me/google/link",
                new LinkGoogleRequest(await _auth.NewCredentialAsync("sub-iso", "iso@gmail.com")))).StatusCode);

        var sessionB = await _auth.LoginSessionAsync(b.Email, AuthTestHelper.Password, b.Slug);
        var methodsB = (await (await _auth.Authed(sessionB.AccessToken).GetAsync("/api/v1/me/sign-in-methods"))
            .Content.ReadFromJsonAsync<SignInMethodsResponse>(JsonOptions))!;

        Assert.False(methodsB.Google.Linked);
        Assert.Equal(
            0,
            await _auth.WithTenantAsync(b.TenantId, db => db.ExternalLogins.CountAsync()));
    }

    private async Task<string> LoginWithHashAsync(TenantAccount account, string email)
    {
        // The invited fixture user has a placeholder hash, so give it a real password through the reset flow.
        using var scope = factory.Services.CreateScope();
        var hasher = scope.ServiceProvider.GetRequiredService<Kadr.Identity.Application.Security.IPasswordHasher>();
        var db = scope.ServiceProvider.GetRequiredService<Kadr.Infrastructure.Persistence.KadrDbContext>();
        scope.ServiceProvider.GetRequiredService<Kadr.SharedKernel.Tenancy.ITenantContextAccessor>().Set(account.TenantId);
        var user = await db.Users.SingleAsync(u => u.Email == email);
        user.SetPassword(hasher.HashPassword(AuthTestHelper.Password), factory.Clock);
        await db.SaveChangesAsync();

        var session = await _auth.LoginSessionAsync(email, AuthTestHelper.Password, account.Slug);
        return session.AccessToken;
    }
}
