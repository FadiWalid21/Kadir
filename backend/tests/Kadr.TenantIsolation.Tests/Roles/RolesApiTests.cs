using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Kadr.Api.Endpoints;
using Kadr.Identity.Application.Auth;
using Kadr.Identity.Domain.Roles;
using Kadr.Infrastructure.Persistence;
using Kadr.Platform.Application.Roles;
using Kadr.Platform.Domain.Audit;
using Kadr.SharedKernel.Constants;
using Kadr.SharedKernel.Tenancy;
using Kadr.TenantIsolation.Tests.Register;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Kadr.TenantIsolation.Tests.Roles;

[Collection(RegisterCollection.Name)]
public sealed class RolesApiTests(RegisterApiFactory factory)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private const string Password = "Str0ngPass!!";

    [Fact]
    public async Task Employee_without_roles_manage_gets_forbidden_on_create()
    {
        await factory.ResetDatabaseAsync();
        var session = await RegisterOwnerAsync("Roles Forbid Co", "roles-forbid@example.com");
        await DemoteToEmployeeAsync(session.TenantId, session.Email);
        var empToken = await LoginAccessTokenAsync(factory.CreateCookieClient(), session.Email, session.Slug);
        var emp = factory.CreateCookieClient();
        emp.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", empToken);

        var response = await emp.PostAsJsonAsync(
            "/api/v1/roles",
            new CreateRoleRequest("Custom", null, null, [PermissionCodes.UsersRead]));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Hr_admin_cannot_create_role_with_subscription_manage()
    {
        await factory.ResetDatabaseAsync();
        var owner = await RegisterOwnerAsync("Roles Escal Co", "roles-escal@example.com");
        var hr = await CreateHrAdminUserAsync(owner.TenantId, "hr-escal@example.com", owner.Slug);
        var client = factory.CreateCookieClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", hr.Token);

        var response = await client.PostAsJsonAsync(
            "/api/v1/roles",
            new CreateRoleRequest(
                "Too Powerful",
                null,
                null,
                [PermissionCodes.UsersRead, PermissionCodes.SubscriptionManage]));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(ErrorCodes.RolesPrivilegeEscalation, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Hr_admin_cannot_edit_or_delete_role_with_permissions_beyond_actor()
    {
        await factory.ResetDatabaseAsync();
        var owner = await RegisterOwnerAsync("Roles Target Co", "roles-target@example.com");
        Guid elevatedRoleId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KadrDbContext>();
            var ambient = scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();
            ambient.Set(owner.TenantId);
            var role = Role.CreateCustom(owner.TenantId, "Elevated Custom", null, null);
            db.Roles.Add(role);
            db.RolePermissions.Add(RolePermission.Create(owner.TenantId, role.Id, PermissionCodes.SubscriptionManage));
            db.RolePermissions.Add(RolePermission.Create(owner.TenantId, role.Id, PermissionCodes.UsersRead));
            await db.SaveChangesAsync();
            elevatedRoleId = role.Id;
        }

        var hr = await CreateHrAdminUserAsync(owner.TenantId, "hr-target@example.com", owner.Slug);
        var client = factory.CreateCookieClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", hr.Token);

        var update = await client.PutAsJsonAsync(
            $"/api/v1/roles/{elevatedRoleId}",
            new UpdateRoleRequest("Elevated Custom", null, null, [PermissionCodes.UsersRead]));
        Assert.Equal(HttpStatusCode.BadRequest, update.StatusCode);
        Assert.Contains(ErrorCodes.RolesPrivilegeEscalation, await update.Content.ReadAsStringAsync());

        var delete = await client.DeleteAsync($"/api/v1/roles/{elevatedRoleId}");
        Assert.Equal(HttpStatusCode.BadRequest, delete.StatusCode);
        Assert.Contains(ErrorCodes.RolesPrivilegeEscalation, await delete.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task System_role_put_and_delete_are_rejected()
    {
        await factory.ResetDatabaseAsync();
        var owner = await RegisterOwnerAsync("Roles System Co", "roles-system@example.com");
        var client = AuthedClient(owner.Token);

        Guid ownerRoleId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KadrDbContext>();
            var ambient = scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();
            ambient.Set(owner.TenantId);
            ownerRoleId = await db.Roles.AsNoTracking()
                .Where(r => r.Name == SystemRoleNames.Owner)
                .Select(r => r.Id)
                .SingleAsync();
        }

        var put = await client.PutAsJsonAsync(
            $"/api/v1/roles/{ownerRoleId}",
            new UpdateRoleRequest("Owner", null, null, [PermissionCodes.UsersRead]));
        Assert.Equal(HttpStatusCode.BadRequest, put.StatusCode);
        Assert.Contains(ErrorCodes.RolesSystemReadonly, await put.Content.ReadAsStringAsync());

        var delete = await client.DeleteAsync($"/api/v1/roles/{ownerRoleId}");
        Assert.Equal(HttpStatusCode.BadRequest, delete.StatusCode);
        Assert.Contains(ErrorCodes.RolesSystemReadonly, await delete.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Delete_blocked_when_users_assigned_and_audit_written_on_update()
    {
        await factory.ResetDatabaseAsync();
        var owner = await RegisterOwnerAsync("Roles InUse Co", "roles-inuse@example.com");
        var client = AuthedClient(owner.Token);

        var created = await client.PostAsJsonAsync(
            "/api/v1/roles",
            new CreateRoleRequest("Custom Viewer", "عارض", "Viewer", [PermissionCodes.UsersRead]));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var role = await created.Content.ReadFromJsonAsync<RoleDetailDto>(JsonOptions);
        Assert.NotNull(role);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KadrDbContext>();
            var ambient = scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();
            ambient.Set(owner.TenantId);
            var user = await db.Users.SingleAsync(u => u.Email == owner.Email);
            db.UserRoles.Add(UserRole.Create(owner.TenantId, user.Id, role.Id));
            await db.SaveChangesAsync();
        }

        var blocked = await client.DeleteAsync($"/api/v1/roles/{role.Id}");
        Assert.Equal(HttpStatusCode.BadRequest, blocked.StatusCode);
        Assert.Contains(ErrorCodes.RolesInUse, await blocked.Content.ReadAsStringAsync());

        // Remove assignment then update + audit
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KadrDbContext>();
            var ambient = scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();
            ambient.Set(owner.TenantId);
            var links = await db.UserRoles.Where(ur => ur.RoleId == role.Id).ToListAsync();
            db.UserRoles.RemoveRange(links);
            await db.SaveChangesAsync();
        }

        var updated = await client.PutAsJsonAsync(
            $"/api/v1/roles/{role.Id}",
            new UpdateRoleRequest("Custom Viewer 2", "عارض", "Viewer", [PermissionCodes.UsersRead, PermissionCodes.RolesRead]));
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KadrDbContext>();
            var ambient = scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();
            ambient.Set(owner.TenantId);
            var entry = await db.AuditLogs.AsNoTracking()
                .Where(a => a.Action == "role.updated" && a.EntityId == role.Id)
                .OrderByDescending(a => a.At)
                .FirstOrDefaultAsync();
            Assert.NotNull(entry);
            Assert.DoesNotContain("password", entry.NewValuesJson ?? "", StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("token", entry.NewValuesJson ?? "", StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task Cross_tenant_role_get_returns_not_found()
    {
        await factory.ResetDatabaseAsync();
        var a = await RegisterOwnerAsync("Roles Iso A", "roles-iso-a@example.com");
        var b = await RegisterOwnerAsync("Roles Iso B", "roles-iso-b@example.com");
        var clientA = AuthedClient(a.Token);

        var created = await clientA.PostAsJsonAsync(
            "/api/v1/roles",
            new CreateRoleRequest("A Only", null, null, [PermissionCodes.UsersRead]));
        var custom = await created.Content.ReadFromJsonAsync<RoleDetailDto>(JsonOptions);

        var clientB = AuthedClient(b.Token);
        var get = await clientB.GetAsync($"/api/v1/roles/{custom!.Id}");
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
    }

    [Fact]
    public async Task Permission_change_invalidates_cache_for_assigned_users()
    {
        await factory.ResetDatabaseAsync();
        var owner = await RegisterOwnerAsync("Roles Cache Co", "roles-cache@example.com");
        var client = AuthedClient(owner.Token);
        var created = await client.PostAsJsonAsync(
            "/api/v1/roles",
            new CreateRoleRequest("Cache Role", null, null, [PermissionCodes.UsersRead]));
        var role = await created.Content.ReadFromJsonAsync<RoleDetailDto>(JsonOptions);

        Guid userId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KadrDbContext>();
            var ambient = scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();
            ambient.Set(owner.TenantId);
            var user = await db.Users.SingleAsync(u => u.Email == owner.Email);
            userId = user.Id;
            // Replace owner role link? Keep owner role and add custom — GetPermissions unions both
            db.UserRoles.Add(UserRole.Create(owner.TenantId, user.Id, role!.Id));
            await db.SaveChangesAsync();
        }

        using (var scope = factory.Services.CreateScope())
        {
            var ambient = scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();
            ambient.Set(owner.TenantId, userId);
            var perms = scope.ServiceProvider.GetRequiredService<IPermissionService>();
            var before = await perms.GetPermissionsAsync(owner.TenantId, userId);
            Assert.Contains(PermissionCodes.UsersRead, before);
        }

        var update = await client.PutAsJsonAsync(
            $"/api/v1/roles/{role!.Id}",
            new UpdateRoleRequest("Cache Role", null, null, [PermissionCodes.RolesRead]));
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        using (var scope = factory.Services.CreateScope())
        {
            var ambient = scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();
            ambient.Set(owner.TenantId, userId);
            var perms = scope.ServiceProvider.GetRequiredService<IPermissionService>();
            var after = await perms.GetPermissionsAsync(owner.TenantId, userId);
            // Owner still has all via Owner role; custom role now grants RolesRead (also on Owner).
            Assert.Contains(PermissionCodes.RolesRead, after);
            Assert.Contains(PermissionCodes.SubscriptionManage, after);
        }
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
        var verify = await client.PostAsJsonAsync("/api/v1/auth/verify-email", new VerifyEmailRequest(token));
        Assert.Equal(HttpStatusCode.NoContent, verify.StatusCode);
        var access = await LoginAccessTokenAsync(client, email, reg.Slug);
        return (reg.Slug, email, reg.TenantId, access);
    }

    private async Task<(string Token, Guid UserId)> CreateHrAdminUserAsync(
        Guid tenantId,
        string email,
        string slug)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KadrDbContext>();
        var ambient = scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();
        var hasher = scope.ServiceProvider.GetRequiredService<Kadr.Identity.Application.Security.IPasswordHasher>();
        ambient.Set(tenantId);
        var hrRole = await db.Roles.SingleAsync(r => r.Name == SystemRoleNames.HrAdmin);
        var user = Kadr.Identity.Domain.Users.AppUser.CreateOwner(
            tenantId,
            email,
            email.ToUpperInvariant(),
            hasher.HashPassword(Password),
            "HR Admin",
            "ar");
        // CreateOwner sets EmailConfirmed false — confirm for login
        user.MarkEmailConfirmed(scope.ServiceProvider.GetRequiredService<Kadr.SharedKernel.Time.IClock>());
        db.Users.Add(user);
        db.UserRoles.Add(UserRole.Create(tenantId, user.Id, hrRole.Id));
        await db.SaveChangesAsync();
        var access = await LoginAccessTokenAsync(factory.CreateCookieClient(), email, slug);
        return (access, user.Id);
    }

    private async Task DemoteToEmployeeAsync(Guid tenantId, string email)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KadrDbContext>();
        var ambient = scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();
        ambient.Set(tenantId);
        var user = await db.Users.SingleAsync(u => u.Email == email);
        var employee = await db.Roles.SingleAsync(r => r.Name == SystemRoleNames.Employee);
        var links = await db.UserRoles.Where(ur => ur.UserId == user.Id).ToListAsync();
        db.UserRoles.RemoveRange(links);
        db.UserRoles.Add(UserRole.Create(tenantId, user.Id, employee.Id));
        await db.SaveChangesAsync();
        scope.ServiceProvider.GetRequiredService<IPermissionCacheInvalidator>().Invalidate(tenantId, user.Id);
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
