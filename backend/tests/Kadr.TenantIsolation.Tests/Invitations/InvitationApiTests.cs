using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Kadr.Api.Endpoints;
using Kadr.Identity.Application.Security;
using Kadr.Identity.Domain.Roles;
using Kadr.Identity.Domain.Users;
using Kadr.Infrastructure.Persistence;
using Kadr.Platform.Application.Invitations;
using Kadr.SharedKernel.Constants;
using Kadr.SharedKernel.Tenancy;
using Kadr.SharedKernel.Time;
using Kadr.TenantIsolation.Tests.Register;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Kadr.TenantIsolation.Tests.Invitations;

[Collection(RegisterCollection.Name)]
public sealed class InvitationApiTests(RegisterApiFactory factory)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private const string Password = "Str0ngPass!!";

    [Fact]
    public async Task Invite_sends_email_with_hashed_token_and_accept_creates_user()
    {
        await factory.ResetDatabaseAsync();
        var owner = await RegisterOwnerAsync("Invite Co", "invite-owner@example.com");
        var client = AuthedClient(owner.Token);
        var hrRoleId = await RoleIdAsync(owner.TenantId, SystemRoleNames.HrAdmin);

        factory.Emails.Clear();
        var invite = await client.PostAsJsonAsync(
            "/api/v1/users/invite",
            new InviteUserRequest("new.hire@example.com", hrRoleId));
        Assert.Equal(HttpStatusCode.Created, invite.StatusCode);
        var raw = factory.Emails.ExtractLastInvitationToken();
        Assert.False(string.IsNullOrWhiteSpace(raw));

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KadrDbContext>();
            var ambient = scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();
            ambient.Set(owner.TenantId);
            var row = await db.Invitations.AsNoTracking().SingleAsync();
            Assert.Equal(TokenHashing.Hash(raw!), row.TokenHash);
            Assert.NotEqual(raw, row.TokenHash);
        }

        var validate = await factory.CreateCookieClient().PostAsJsonAsync(
            "/api/v1/auth/invitations/validate",
            new ValidateInvitationRequest(raw!));
        Assert.Equal(HttpStatusCode.OK, validate.StatusCode);
        var validBody = await validate.Content.ReadFromJsonAsync<ValidateInvitationResponse>(JsonOptions);
        Assert.Equal("valid", validBody!.Status);
        Assert.Equal("new.hire@example.com", validBody.Email);
        Assert.Equal("Invite Co", validBody.CompanyName);
        Assert.Equal(SystemRoleNames.HrAdmin, validBody.RoleName);

        Guid ownerUserId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KadrDbContext>();
            var ambient = scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();
            ambient.Set(owner.TenantId);
            var row = await db.Invitations.AsNoTracking().SingleAsync();
            Assert.NotNull(row.CreatedBy);
            ownerUserId = row.CreatedBy!.Value;
        }

        var accept = await factory.CreateCookieClient().PostAsJsonAsync(
            "/api/v1/auth/accept-invitation",
            new AcceptInvitationRequest(raw!, "New Hire", Password));
        Assert.Equal(HttpStatusCode.NoContent, accept.StatusCode);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KadrDbContext>();
            var ambient = scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();
            ambient.Set(owner.TenantId);
            var note = await db.Notifications.AsNoTracking()
                .SingleAsync(n => n.RecipientUserId == ownerUserId && n.Type == "invitation.accepted");
            Assert.Equal("/settings/invitations", note.LinkPath);
            Assert.Contains("New Hire", note.BodyEn, StringComparison.Ordinal);
        }

        var login = await factory.CreateCookieClient().PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest("new.hire@example.com", Password, owner.Slug));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        var used = await factory.CreateCookieClient().PostAsJsonAsync(
            "/api/v1/auth/invitations/validate",
            new ValidateInvitationRequest(raw!));
        var usedBody = await used.Content.ReadFromJsonAsync<ValidateInvitationResponse>(JsonOptions);
        Assert.Equal("used", usedBody!.Status);
    }

    [Fact]
    public async Task Accept_skips_inviter_notification_when_CreatedBy_null()
    {
        await factory.ResetDatabaseAsync();
        var owner = await RegisterOwnerAsync("Invite NoSender Co", "invite-nosender@example.com");
        var client = AuthedClient(owner.Token);
        factory.Emails.Clear();
        await client.PostAsJsonAsync(
            "/api/v1/users/invite",
            new InviteUserRequest(
                "orphan.hire@example.com",
                await RoleIdAsync(owner.TenantId, SystemRoleNames.Employee)));
        var raw = factory.Emails.ExtractLastInvitationToken();
        Assert.False(string.IsNullOrWhiteSpace(raw));

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KadrDbContext>();
            var ambient = scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();
            ambient.Set(owner.TenantId);
            await db.Invitations.ExecuteUpdateAsync(s => s.SetProperty(i => i.CreatedBy, (Guid?)null));
        }

        var accept = await factory.CreateCookieClient().PostAsJsonAsync(
            "/api/v1/auth/accept-invitation",
            new AcceptInvitationRequest(raw!, "Orphan Hire", Password));
        Assert.Equal(HttpStatusCode.NoContent, accept.StatusCode);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KadrDbContext>();
            var ambient = scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();
            ambient.Set(owner.TenantId);
            Assert.Equal(
                0,
                await db.Notifications.CountAsync(n => n.Type == "invitation.accepted"));
        }
    }

    [Fact]
    public async Task Validate_unknown_and_garbage_both_return_invalid()
    {
        await factory.ResetDatabaseAsync();
        var client = factory.CreateCookieClient();
        var a = await client.PostAsJsonAsync(
            "/api/v1/auth/invitations/validate",
            new ValidateInvitationRequest("not-a-real-token"));
        var b = await client.PostAsJsonAsync(
            "/api/v1/auth/invitations/validate",
            new ValidateInvitationRequest("also-unknown"));
        var bodyA = await a.Content.ReadFromJsonAsync<ValidateInvitationResponse>(JsonOptions);
        var bodyB = await b.Content.ReadFromJsonAsync<ValidateInvitationResponse>(JsonOptions);
        Assert.Equal("invalid", bodyA!.Status);
        Assert.Equal("invalid", bodyB!.Status);
        Assert.Null(bodyA.Email);
        Assert.Null(bodyB.Email);
    }

    [Fact]
    public async Task Expired_revoked_and_reinvite_update_role()
    {
        await factory.ResetDatabaseAsync();
        var owner = await RegisterOwnerAsync("Invite States Co", "invite-states@example.com");
        var client = AuthedClient(owner.Token);
        var hrRoleId = await RoleIdAsync(owner.TenantId, SystemRoleNames.HrAdmin);
        var empRoleId = await RoleIdAsync(owner.TenantId, SystemRoleNames.Employee);

        factory.Emails.Clear();
        var created = await client.PostAsJsonAsync(
            "/api/v1/users/invite",
            new InviteUserRequest("pending@example.com", hrRoleId));
        var item = await created.Content.ReadFromJsonAsync<InvitationListItemDto>(JsonOptions);
        var firstToken = factory.Emails.ExtractLastInvitationToken();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KadrDbContext>();
            var ambient = scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();
            ambient.Set(owner.TenantId);
            await db.Invitations
                .Where(i => i.Id == item!.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(i => i.ExpiresAt, DateTimeOffset.UtcNow.AddDays(-1)));
        }

        var expired = await factory.CreateCookieClient().PostAsJsonAsync(
            "/api/v1/auth/invitations/validate",
            new ValidateInvitationRequest(firstToken!));
        Assert.Equal(
            "expired",
            (await expired.Content.ReadFromJsonAsync<ValidateInvitationResponse>(JsonOptions))!.Status);

        // Fresh invite then revoke
        factory.Emails.Clear();
        await client.PostAsJsonAsync("/api/v1/users/invite", new InviteUserRequest("pending@example.com", hrRoleId));
        var list = await client.GetFromJsonAsync<List<InvitationListItemDto>>("/api/v1/invitations", JsonOptions);
        var pendingId = list!.Single().Id;
        var revokeToken = factory.Emails.ExtractLastInvitationToken();
        var revoke = await client.PostAsync($"/api/v1/invitations/{pendingId}/revoke", null);
        Assert.Equal(HttpStatusCode.NoContent, revoke.StatusCode);
        var revoked = await factory.CreateCookieClient().PostAsJsonAsync(
            "/api/v1/auth/invitations/validate",
            new ValidateInvitationRequest(revokeToken!));
        Assert.Equal(
            "revoked",
            (await revoked.Content.ReadFromJsonAsync<ValidateInvitationResponse>(JsonOptions))!.Status);

        // Re-invite same email updates role (create new after revoke)
        factory.Emails.Clear();
        var reinvite = await client.PostAsJsonAsync(
            "/api/v1/users/invite",
            new InviteUserRequest("pending@example.com", empRoleId));
        Assert.Equal(HttpStatusCode.Created, reinvite.StatusCode);
        var reBody = await reinvite.Content.ReadFromJsonAsync<InvitationListItemDto>(JsonOptions);
        Assert.Equal(empRoleId, reBody!.RoleId);
    }

    [Fact]
    public async Task Deactivated_user_email_still_email_taken_on_invite()
    {
        await factory.ResetDatabaseAsync();
        var owner = await RegisterOwnerAsync("Invite Taken Co", "invite-taken@example.com");
        var hr = await CreateHrAsync(owner.TenantId, "taken@example.com", owner.Slug);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KadrDbContext>();
            var ambient = scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();
            ambient.Set(owner.TenantId);
            var user = await db.Users.SingleAsync(u => u.Id == hr.UserId);
            user.SetActive(false);
            await db.SaveChangesAsync();
        }

        var client = AuthedClient(owner.Token);
        var response = await client.PostAsJsonAsync(
            "/api/v1/users/invite",
            new InviteUserRequest("taken@example.com", await RoleIdAsync(owner.TenantId, SystemRoleNames.Employee)));
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains(ErrorCodes.AuthEmailTaken, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Delete_role_blocked_when_pending_invitation_references_it()
    {
        await factory.ResetDatabaseAsync();
        var owner = await RegisterOwnerAsync("Invite RoleInUse Co", "invite-roleinuse@example.com");
        var client = AuthedClient(owner.Token);
        var created = await client.PostAsJsonAsync(
            "/api/v1/roles",
            new Kadr.Platform.Application.Roles.CreateRoleRequest(
                "Invite Only Role",
                null,
                null,
                [PermissionCodes.UsersRead]));
        var role = await created.Content.ReadFromJsonAsync<Kadr.Platform.Application.Roles.RoleDetailDto>(JsonOptions);
        await client.PostAsJsonAsync(
            "/api/v1/users/invite",
            new InviteUserRequest("role-inuse@example.com", role!.Id));

        var delete = await client.DeleteAsync($"/api/v1/roles/{role.Id}");
        Assert.Equal(HttpStatusCode.BadRequest, delete.StatusCode);
        Assert.Contains(ErrorCodes.RolesInUse, await delete.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Concurrent_accept_creates_only_one_user()
    {
        await factory.ResetDatabaseAsync();
        var owner = await RegisterOwnerAsync("Invite Race Co", "invite-race@example.com");
        var client = AuthedClient(owner.Token);
        factory.Emails.Clear();
        await client.PostAsJsonAsync(
            "/api/v1/users/invite",
            new InviteUserRequest("race@example.com", await RoleIdAsync(owner.TenantId, SystemRoleNames.Employee)));
        var token = factory.Emails.ExtractLastInvitationToken();

        var anon1 = factory.CreateCookieClient();
        var anon2 = factory.CreateCookieClient();
        var req = new AcceptInvitationRequest(token!, "Racer", Password);
        var t1 = anon1.PostAsJsonAsync("/api/v1/auth/accept-invitation", req);
        var t2 = anon2.PostAsJsonAsync("/api/v1/auth/accept-invitation", req);
        var results = await Task.WhenAll(t1, t2);
        Assert.Contains(results, r => r.StatusCode == HttpStatusCode.NoContent);
        Assert.Contains(results, r => r.StatusCode == HttpStatusCode.BadRequest);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KadrDbContext>();
        var ambient = scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();
        ambient.Set(owner.TenantId);
        Assert.Equal(1, await db.Users.CountAsync(u => u.Email == "race@example.com"));
    }

    [Fact]
    public async Task Me_checklist_true_when_pending_invitation_exists()
    {
        await factory.ResetDatabaseAsync();
        var owner = await RegisterOwnerAsync("Invite Check Co", "invite-check@example.com");
        var client = AuthedClient(owner.Token);
        await client.PostAsJsonAsync(
            "/api/v1/users/invite",
            new InviteUserRequest("check@example.com", await RoleIdAsync(owner.TenantId, SystemRoleNames.Employee)));
        var me = await client.GetFromJsonAsync<MeResponse>("/api/v1/me", JsonOptions);
        Assert.True(me!.Checklist.InviteTeamDone);
    }

    private async Task<(string Slug, string Email, Guid TenantId, string Token)> RegisterOwnerAsync(
        string company,
        string email)
    {
        var client = factory.CreateCookieClient();
        factory.Emails.Clear();
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/register")
        {
            Content = JsonContent.Create(new RegisterTenantRequest(
                "Owner",
                email,
                Password,
                company,
                "office",
                "Africa/Cairo",
                "ar",
                "EG")),
        };
        request.Headers.TryAddWithoutValidation(HttpHeaderNames.IdempotencyKey, Guid.NewGuid().ToString("N"));
        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var reg = await response.Content.ReadFromJsonAsync<RegisterTenantResponse>(JsonOptions);
        Assert.NotNull(reg);
        var token = factory.Emails.ExtractLastVerificationToken();
        Assert.NotNull(token);
        await client.PostAsJsonAsync("/api/v1/auth/verify-email", new VerifyEmailRequest(token));
        var access = await LoginAccessTokenAsync(client, email, reg.Slug);
        return (reg.Slug, email, reg.TenantId, access);
    }

    private async Task<(string Token, Guid UserId)> CreateHrAsync(Guid tenantId, string email, string slug)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KadrDbContext>();
        var ambient = scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        ambient.Set(tenantId);
        var hrRole = await db.Roles.SingleAsync(r => r.Name == SystemRoleNames.HrAdmin);
        var user = AppUser.CreateOwner(
            tenantId,
            email,
            email.ToUpperInvariant(),
            hasher.HashPassword(Password),
            "HR",
            "ar");
        user.MarkEmailConfirmed(scope.ServiceProvider.GetRequiredService<IClock>());
        db.Users.Add(user);
        db.UserRoles.Add(UserRole.Create(tenantId, user.Id, hrRole.Id));
        await db.SaveChangesAsync();
        return (await LoginAccessTokenAsync(factory.CreateCookieClient(), email, slug), user.Id);
    }

    private async Task<Guid> RoleIdAsync(Guid tenantId, string name)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KadrDbContext>();
        var ambient = scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();
        ambient.Set(tenantId);
        return await db.Roles.AsNoTracking().Where(r => r.Name == name).Select(r => r.Id).SingleAsync();
    }

    private HttpClient AuthedClient(string token)
    {
        var client = factory.CreateCookieClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static async Task<string> LoginAccessTokenAsync(HttpClient client, string email, string slug)
    {
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, Password, slug));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var body = await login.Content.ReadFromJsonAsync<AccessTokenResponse>(JsonOptions);
        return body!.AccessToken;
    }
}
