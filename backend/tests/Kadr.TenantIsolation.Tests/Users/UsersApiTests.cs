using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Kadr.Api.Endpoints;
using Kadr.Identity.Application.Auth;
using Kadr.Identity.Domain.Roles;
using Kadr.Infrastructure.Persistence;
using Kadr.Platform.Application.Users;
using Kadr.SharedKernel.Constants;
using Kadr.SharedKernel.Tenancy;
using DataScope = Kadr.SharedKernel.Tenancy.DataScope;
using Kadr.TenantIsolation.Tests.Register;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Kadr.TenantIsolation.Tests.Users;

[Collection(RegisterCollection.Name)]
public sealed class UsersApiTests(RegisterApiFactory factory)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private const string Password = "Str0ngPass!!";

    [Fact]
    public async Task Employee_without_users_read_gets_forbidden_on_list()
    {
        await factory.ResetDatabaseAsync();
        var session = await RegisterOwnerAsync("Users Forbid Co", "users-forbid@example.com");
        await DemoteToEmployeeAsync(session.TenantId, session.Email);
        var empToken = await LoginAccessTokenAsync(factory.CreateCookieClient(), session.Email, session.Slug);
        var emp = AuthedClient(empToken);

        var response = await emp.GetAsync("/api/v1/users");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Hr_admin_cannot_demote_or_deactivate_owner()
    {
        await factory.ResetDatabaseAsync();
        var owner = await RegisterOwnerAsync("Users Escal Co", "users-escal@example.com");
        var hr = await CreateHrAdminUserAsync(owner.TenantId, "hr-users@example.com", owner.Slug);

        Guid ownerUserId;
        Guid employeeRoleId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KadrDbContext>();
            var ambient = scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();
            ambient.Set(owner.TenantId);
            ownerUserId = await db.Users.AsNoTracking()
                .Where(u => u.Email == owner.Email)
                .Select(u => u.Id)
                .SingleAsync();
            employeeRoleId = await db.Roles.AsNoTracking()
                .Where(r => r.Name == SystemRoleNames.Employee)
                .Select(r => r.Id)
                .SingleAsync();
        }

        var client = AuthedClient(hr.Token);
        var demote = await client.PutAsJsonAsync(
            $"/api/v1/users/{ownerUserId}",
            new UpdateUserRequest("Owner", employeeRoleId, true, DataScope.All));
        Assert.Equal(HttpStatusCode.BadRequest, demote.StatusCode);
        Assert.Contains(ErrorCodes.UsersPrivilegeEscalation, await demote.Content.ReadAsStringAsync());

        var deactivate = await client.PutAsJsonAsync(
            $"/api/v1/users/{ownerUserId}",
            new UpdateUserRequest("Owner", await OwnerRoleIdAsync(owner.TenantId), false, DataScope.All));
        Assert.Equal(HttpStatusCode.BadRequest, deactivate.StatusCode);
        Assert.Contains(ErrorCodes.UsersPrivilegeEscalation, await deactivate.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Self_deactivation_is_forbidden()
    {
        await factory.ResetDatabaseAsync();
        var owner = await RegisterOwnerAsync("Users Self Co", "users-self@example.com");
        var hr = await CreateHrAdminUserAsync(owner.TenantId, "hr-self@example.com", owner.Slug);
        var client = AuthedClient(hr.Token);

        var response = await client.PutAsJsonAsync(
            $"/api/v1/users/{hr.UserId}",
            new UpdateUserRequest("HR Admin", await HrRoleIdAsync(owner.TenantId), false, DataScope.All));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(ErrorCodes.UsersSelfDeactivationForbidden, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Last_active_owner_cannot_be_demoted_or_deactivated()
    {
        await factory.ResetDatabaseAsync();
        var owner = await RegisterOwnerAsync("Users LastOwner Co", "users-lastowner@example.com");
        var client = AuthedClient(owner.Token);

        Guid ownerUserId;
        Guid hrRoleId;
        Guid fullPermsRoleId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KadrDbContext>();
            var ambient = scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();
            ambient.Set(owner.TenantId);
            ownerUserId = await db.Users.AsNoTracking()
                .Where(u => u.Email == owner.Email)
                .Select(u => u.Id)
                .SingleAsync();
            hrRoleId = await db.Roles.AsNoTracking()
                .Where(r => r.Name == SystemRoleNames.HrAdmin)
                .Select(r => r.Id)
                .SingleAsync();

            // Peer with all permissions (not Owner role) can pass target-privilege but not last-owner.
            var full = Role.CreateCustom(owner.TenantId, "Full Custom", null, null);
            db.Roles.Add(full);
            foreach (var code in PermissionCodes.All)
            {
                db.RolePermissions.Add(RolePermission.Create(owner.TenantId, full.Id, code));
            }

            await db.SaveChangesAsync();
            fullPermsRoleId = full.Id;
        }

        var demote = await client.PutAsJsonAsync(
            $"/api/v1/users/{ownerUserId}",
            new UpdateUserRequest("Owner", hrRoleId, true, DataScope.All));
        Assert.Equal(HttpStatusCode.BadRequest, demote.StatusCode);
        Assert.Contains(ErrorCodes.UsersLastOwnerProtected, await demote.Content.ReadAsStringAsync());

        var peer = await CreateUserWithRoleAsync(
            owner.TenantId,
            "peer-lastowner@example.com",
            owner.Slug,
            fullPermsRoleId,
            "Peer");
        var peerClient = AuthedClient(peer.Token);
        var deactivate = await peerClient.PutAsJsonAsync(
            $"/api/v1/users/{ownerUserId}",
            new UpdateUserRequest("Owner", await OwnerRoleIdAsync(owner.TenantId), false, DataScope.All));
        Assert.Equal(HttpStatusCode.BadRequest, deactivate.StatusCode);
        Assert.Contains(ErrorCodes.UsersLastOwnerProtected, await deactivate.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Deactivate_revokes_refresh_and_clears_permissions_for_access_token()
    {
        await factory.ResetDatabaseAsync();
        var owner = await RegisterOwnerAsync("Users Deact Co", "users-deact@example.com");
        var hr = await CreateHrAdminUserAsync(owner.TenantId, "hr-deact@example.com", owner.Slug);

        // Warm permission cache while active
        using (var scope = factory.Services.CreateScope())
        {
            var ambient = scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();
            ambient.Set(owner.TenantId, hr.UserId);
            var perms = await scope.ServiceProvider.GetRequiredService<IPermissionService>()
                .GetPermissionsAsync(owner.TenantId, hr.UserId);
            Assert.Contains(PermissionCodes.UsersRead, perms);
        }

        var ownerClient = AuthedClient(owner.Token);
        var deactivate = await ownerClient.PutAsJsonAsync(
            $"/api/v1/users/{hr.UserId}",
            new UpdateUserRequest("HR Admin", await HrRoleIdAsync(owner.TenantId), false, DataScope.All));
        Assert.Equal(HttpStatusCode.OK, deactivate.StatusCode);

        using (var scope = factory.Services.CreateScope())
        {
            var ambient = scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();
            ambient.Set(owner.TenantId, hr.UserId);
            var perms = await scope.ServiceProvider.GetRequiredService<IPermissionService>()
                .GetPermissionsAsync(owner.TenantId, hr.UserId);
            Assert.Empty(perms);

            var db = scope.ServiceProvider.GetRequiredService<KadrDbContext>();
            var refreshAlive = await db.RefreshTokens.AsNoTracking()
                .AnyAsync(t => t.UserId == hr.UserId && t.RevokedAt == null);
            Assert.False(refreshAlive);
        }

        var hrClient = AuthedClient(hr.Token);
        var list = await hrClient.GetAsync("/api/v1/users");
        Assert.Equal(HttpStatusCode.Forbidden, list.StatusCode);
    }

    [Fact]
    public async Task Cross_tenant_user_get_returns_not_found()
    {
        await factory.ResetDatabaseAsync();
        var a = await RegisterOwnerAsync("Users Iso A", "users-iso-a@example.com");
        var b = await RegisterOwnerAsync("Users Iso B", "users-iso-b@example.com");

        Guid aUserId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KadrDbContext>();
            var ambient = scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();
            ambient.Set(a.TenantId);
            aUserId = await db.Users.AsNoTracking()
                .Where(u => u.Email == a.Email)
                .Select(u => u.Id)
                .SingleAsync();
        }

        var clientB = AuthedClient(b.Token);
        var get = await clientB.GetAsync($"/api/v1/users/{aUserId}");
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
    }

    [Fact]
    public async Task Me_checklist_invite_team_done_when_other_user_exists()
    {
        await factory.ResetDatabaseAsync();
        var owner = await RegisterOwnerAsync("Users Check Co", "users-check@example.com");
        var client = AuthedClient(owner.Token);

        var before = await client.GetAsync("/api/v1/me");
        var beforeBody = await before.Content.ReadFromJsonAsync<MeResponse>(JsonOptions);
        Assert.False(beforeBody!.Checklist.InviteTeamDone);

        await CreateHrAdminUserAsync(owner.TenantId, "hr-check@example.com", owner.Slug);
        var after = await client.GetAsync("/api/v1/me");
        var afterBody = await after.Content.ReadFromJsonAsync<MeResponse>(JsonOptions);
        Assert.True(afterBody!.Checklist.InviteTeamDone);
    }

    [Fact]
    public async Task Role_change_writes_audit_without_secrets()
    {
        await factory.ResetDatabaseAsync();
        var owner = await RegisterOwnerAsync("Users Audit Co", "users-audit@example.com");
        var hr = await CreateHrAdminUserAsync(owner.TenantId, "hr-audit@example.com", owner.Slug);
        var client = AuthedClient(owner.Token);
        Guid employeeRoleId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KadrDbContext>();
            var ambient = scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();
            ambient.Set(owner.TenantId);
            employeeRoleId = await db.Roles.AsNoTracking()
                .Where(r => r.Name == SystemRoleNames.Employee)
                .Select(r => r.Id)
                .SingleAsync();
        }

        var update = await client.PutAsJsonAsync(
            $"/api/v1/users/{hr.UserId}",
            new UpdateUserRequest("HR Admin", employeeRoleId, true, DataScope.All));
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KadrDbContext>();
            var ambient = scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();
            ambient.Set(owner.TenantId);
            var entry = await db.AuditLogs.AsNoTracking()
                .Where(a => a.Action == "user.role_changed" && a.EntityId == hr.UserId)
                .OrderByDescending(a => a.At)
                .FirstOrDefaultAsync();
            Assert.NotNull(entry);
            Assert.DoesNotContain("password", entry.NewValuesJson ?? "", StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("token", entry.NewValuesJson ?? "", StringComparison.OrdinalIgnoreCase);
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
        ambient.Set(tenantId);
        var hrRole = await db.Roles.SingleAsync(r => r.Name == SystemRoleNames.HrAdmin);
        return await CreateUserWithRoleAsync(tenantId, email, slug, hrRole.Id, "HR Admin");
    }

    private async Task<(string Token, Guid UserId)> CreateUserWithRoleAsync(
        Guid tenantId,
        string email,
        string slug,
        Guid roleId,
        string fullName)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KadrDbContext>();
        var ambient = scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();
        var hasher = scope.ServiceProvider.GetRequiredService<Kadr.Identity.Application.Security.IPasswordHasher>();
        ambient.Set(tenantId);
        var user = Kadr.Identity.Domain.Users.AppUser.CreateOwner(
            tenantId,
            email,
            email.ToUpperInvariant(),
            hasher.HashPassword(Password),
            fullName,
            "ar");
        user.MarkEmailConfirmed(scope.ServiceProvider.GetRequiredService<Kadr.SharedKernel.Time.IClock>());
        db.Users.Add(user);
        db.UserRoles.Add(UserRole.Create(tenantId, user.Id, roleId));
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

    [Fact]
    public async Task Link_and_unlink_user_employee_is_bidirectional()
    {
        await factory.ResetDatabaseAsync();
        var owner = await RegisterOwnerAsync("Users Link Co", "users-link@example.com");
        var client = AuthedClient(owner.Token);

        var create = await client.PostAsJsonAsync(
            "/api/v1/employees",
            new
            {
                fullNameAr = "موظف مربوط",
                fullNameEn = "Linked Emp",
                hireDate = "2024-01-15",
                status = "Active",
                email = "linked.emp@example.com",
            });
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var emp = (await create.Content.ReadFromJsonAsync<Kadr.Platform.Application.Employees.EmployeeDetailDto>(JsonOptions))!;

        Guid ownerUserId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KadrDbContext>();
            var ambient = scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();
            ambient.Set(owner.TenantId);
            ownerUserId = await db.Users.AsNoTracking()
                .Where(u => u.Email == owner.Email)
                .Select(u => u.Id)
                .SingleAsync();
        }

        var link = await client.PutAsJsonAsync(
            $"/api/v1/users/{ownerUserId}/employee",
            new SetUserEmployeeRequest(emp.Id));
        Assert.Equal(HttpStatusCode.OK, link.StatusCode);
        var linked = (await link.Content.ReadFromJsonAsync<UserDetailDto>(JsonOptions))!;
        Assert.Equal(emp.Id, linked.EmployeeId);
        Assert.Equal("موظف مربوط", linked.EmployeeNameAr);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KadrDbContext>();
            var ambient = scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();
            ambient.Set(owner.TenantId);
            var employee = await db.Employees.AsNoTracking().SingleAsync(e => e.Id == emp.Id);
            Assert.Equal(ownerUserId, employee.UserId);
        }

        var second = await client.PostAsJsonAsync(
            "/api/v1/employees",
            new
            {
                fullNameAr = "موظف آخر",
                hireDate = "2024-02-01",
                status = "Active",
            });
        var emp2 = (await second.Content.ReadFromJsonAsync<Kadr.Platform.Application.Employees.EmployeeDetailDto>(JsonOptions))!;
        var conflict = await client.PutAsJsonAsync(
            $"/api/v1/users/{ownerUserId}/employee",
            new SetUserEmployeeRequest(emp2.Id));
        Assert.Equal(HttpStatusCode.BadRequest, conflict.StatusCode);
        Assert.Contains(ErrorCodes.UsersEmployeeAlreadyLinked, await conflict.Content.ReadAsStringAsync());

        var unlink = await client.PutAsJsonAsync(
            $"/api/v1/users/{ownerUserId}/employee",
            new SetUserEmployeeRequest(null));
        Assert.Equal(HttpStatusCode.OK, unlink.StatusCode);
        var unlinked = (await unlink.Content.ReadFromJsonAsync<UserDetailDto>(JsonOptions))!;
        Assert.Null(unlinked.EmployeeId);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KadrDbContext>();
            var ambient = scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();
            ambient.Set(owner.TenantId);
            Assert.Null(await db.Employees.AsNoTracking().Where(e => e.Id == emp.Id).Select(e => e.UserId).SingleAsync());
            Assert.Null(await db.Users.AsNoTracking().Where(u => u.Id == ownerUserId).Select(u => u.EmployeeId).SingleAsync());
        }
    }

    [Fact]
    public async Task Link_employee_already_linked_to_other_user_conflicts()
    {
        await factory.ResetDatabaseAsync();
        var owner = await RegisterOwnerAsync("Users Link Conflict Co", "users-link-conflict@example.com");
        var hr = await CreateHrAdminUserAsync(owner.TenantId, "hr-link-conflict@example.com", owner.Slug);
        var client = AuthedClient(owner.Token);

        var create = await client.PostAsJsonAsync(
            "/api/v1/employees",
            new { fullNameAr = "مشترك", hireDate = "2024-01-15", status = "Active" });
        var emp = (await create.Content.ReadFromJsonAsync<Kadr.Platform.Application.Employees.EmployeeDetailDto>(JsonOptions))!;

        var linkOwner = await client.PutAsJsonAsync(
            $"/api/v1/users/{(await OwnerUserIdAsync(owner.TenantId, owner.Email))}/employee",
            new SetUserEmployeeRequest(emp.Id));
        Assert.Equal(HttpStatusCode.OK, linkOwner.StatusCode);

        var linkHr = await client.PutAsJsonAsync(
            $"/api/v1/users/{hr.UserId}/employee",
            new SetUserEmployeeRequest(emp.Id));
        Assert.Equal(HttpStatusCode.BadRequest, linkHr.StatusCode);
        Assert.Contains(ErrorCodes.UsersEmployeeLinkConflict, await linkHr.Content.ReadAsStringAsync());
    }

    private async Task<Guid> OwnerUserIdAsync(Guid tenantId, string email)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KadrDbContext>();
        var ambient = scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();
        ambient.Set(tenantId);
        return await db.Users.AsNoTracking()
            .Where(u => u.Email == email)
            .Select(u => u.Id)
            .SingleAsync();
    }

    private async Task<Guid> OwnerRoleIdAsync(Guid tenantId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KadrDbContext>();
        var ambient = scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();
        ambient.Set(tenantId);
        return await db.Roles.AsNoTracking()
            .Where(r => r.Name == SystemRoleNames.Owner)
            .Select(r => r.Id)
            .SingleAsync();
    }

    private async Task<Guid> HrRoleIdAsync(Guid tenantId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KadrDbContext>();
        var ambient = scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();
        ambient.Set(tenantId);
        return await db.Roles.AsNoTracking()
            .Where(r => r.Name == SystemRoleNames.HrAdmin)
            .Select(r => r.Id)
            .SingleAsync();
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
