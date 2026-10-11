using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Kadr.Api.Endpoints;
using Kadr.Identity.Application.Auth;
using Kadr.Identity.Domain.Roles;
using Kadr.Infrastructure.Persistence;
using Kadr.Platform.Application.Audit;
using Kadr.Platform.Application.Roles;
using Kadr.SharedKernel.Constants;
using Kadr.SharedKernel.Tenancy;
using Kadr.TenantIsolation.Tests.Register;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Kadr.TenantIsolation.Tests.Audit;

[Collection(RegisterCollection.Name)]
public sealed class AuditApiTests(RegisterApiFactory factory)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private const string Password = "Str0ngPass!!";

    [Fact]
    public async Task Employee_without_audit_read_gets_forbidden()
    {
        await factory.ResetDatabaseAsync();
        var session = await RegisterOwnerAsync("Audit Forbid Co", "audit-forbid@example.com");
        await DemoteToEmployeeAsync(session.TenantId, session.Email);
        var empToken = await LoginAccessTokenAsync(factory.CreateCookieClient(), session.Email, session.Slug);
        var emp = AuthedClient(empToken);

        var list = await emp.GetAsync("/api/v1/audit-logs");
        var export = await emp.GetAsync("/api/v1/audit-logs/export");
        Assert.Equal(HttpStatusCode.Forbidden, list.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, export.StatusCode);
    }

    [Fact]
    public async Task Login_failed_and_lock_are_audited_without_secrets()
    {
        await factory.ResetDatabaseAsync();
        var session = await RegisterOwnerAsync("Audit Login Co", "audit-login@example.com");
        var client = factory.CreateCookieClient();

        var fail = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest(session.Email, "WrongPass!!x", session.Slug));
        Assert.Equal(HttpStatusCode.Unauthorized, fail.StatusCode);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KadrDbContext>();
            var ambient = scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();
            ambient.Set(session.TenantId);
            var entry = await db.AuditLogs.AsNoTracking()
                .Where(a => a.Action == AuditActions.AuthLoginFailed)
                .OrderByDescending(a => a.At)
                .FirstOrDefaultAsync();
            Assert.NotNull(entry);
            Assert.Contains(session.Email.ToUpperInvariant(), entry.NewValuesJson ?? "", StringComparison.Ordinal);
            Assert.DoesNotContain("WrongPass", entry.NewValuesJson ?? "", StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("password", entry.NewValuesJson ?? "", StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("PasswordHash", entry.OldValuesJson ?? "", StringComparison.OrdinalIgnoreCase);
        }

        for (var i = 0; i < 4; i++)
        {
            await client.PostAsJsonAsync(
                "/api/v1/auth/login",
                new LoginRequest(session.Email, "WrongPass!!x", session.Slug));
        }

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KadrDbContext>();
            var ambient = scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();
            ambient.Set(session.TenantId);
            var locked = await db.AuditLogs.AsNoTracking()
                .AnyAsync(a => a.Action == AuditActions.AuthAccountLocked);
            Assert.True(locked);
        }
    }

    [Fact]
    public async Task Role_change_audit_is_returned_by_list_and_never_contains_secrets()
    {
        await factory.ResetDatabaseAsync();
        var owner = await RegisterOwnerAsync("Audit Role Co", "audit-role@example.com");
        var client = AuthedClient(owner.Token);

        var created = await client.PostAsJsonAsync(
            "/api/v1/roles",
            new CreateRoleRequest("Audit Custom", null, null, [PermissionCodes.UsersRead]));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var role = await created.Content.ReadFromJsonAsync<RoleDetailDto>(JsonOptions);

        var updated = await client.PutAsJsonAsync(
            $"/api/v1/roles/{role!.Id}",
            new UpdateRoleRequest(
                "Audit Custom 2",
                null,
                null,
                [PermissionCodes.UsersRead, PermissionCodes.RolesRead]));
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);

        var list = await client.GetFromJsonAsync<AuditLogListResultDto>(
            "/api/v1/audit-logs?action=role.updated&pageSize=10",
            JsonOptions);
        Assert.NotNull(list);
        Assert.Contains(list.Items, i => i.Action == "role.updated" && i.EntityId == role.Id);
        var item = list.Items.First(i => i.Action == "role.updated" && i.EntityId == role.Id);
        Assert.Contains("Audit Custom", item.OldValuesJson ?? "", StringComparison.Ordinal);
        Assert.Contains("Audit Custom 2", item.NewValuesJson ?? "", StringComparison.Ordinal);
        Assert.DoesNotContain("password", item.OldValuesJson ?? "", StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("token", item.NewValuesJson ?? "", StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(item.UserId);
    }

    [Fact]
    public async Task Interceptor_redacts_password_hash_on_user_create()
    {
        await factory.ResetDatabaseAsync();
        var owner = await RegisterOwnerAsync("Audit Redact Co", "audit-redact@example.com");
        await CreateHrAdminUserAsync(owner.TenantId, "hr-redact@example.com", owner.Slug);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KadrDbContext>();
        var ambient = scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();
        ambient.Set(owner.TenantId);
        var auto = await db.AuditLogs.AsNoTracking()
            .Where(a => a.Action == "entity.created" && a.EntityType == "AppUser")
            .OrderByDescending(a => a.At)
            .FirstOrDefaultAsync();
        Assert.NotNull(auto);
        Assert.DoesNotContain("PasswordHash", auto.NewValuesJson ?? "", StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", auto.NewValuesJson ?? "", StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task List_is_tenant_isolated_and_supports_keyset_cursor()
    {
        await factory.ResetDatabaseAsync();
        var a = await RegisterOwnerAsync("Audit Iso A", "audit-iso-a@example.com");
        var b = await RegisterOwnerAsync("Audit Iso B", "audit-iso-b@example.com");
        var clientA = AuthedClient(a.Token);
        var clientB = AuthedClient(b.Token);

        for (var i = 0; i < 3; i++)
        {
            var response = await clientA.PostAsJsonAsync(
                "/api/v1/roles",
                new CreateRoleRequest($"A Role {i}", null, null, [PermissionCodes.UsersRead]));
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }

        await clientB.PostAsJsonAsync(
            "/api/v1/roles",
            new CreateRoleRequest("B Role", null, null, [PermissionCodes.UsersRead]));

        var page1 = await clientA.GetFromJsonAsync<AuditLogListResultDto>(
            "/api/v1/audit-logs?action=role.created&pageSize=2",
            JsonOptions);
        Assert.NotNull(page1);
        Assert.Equal(2, page1.Items.Count);
        Assert.False(string.IsNullOrWhiteSpace(page1.NextCursor));
        Assert.All(page1.Items, i => Assert.Equal("role.created", i.Action));

        var page2 = await clientA.GetFromJsonAsync<AuditLogListResultDto>(
            $"/api/v1/audit-logs?action=role.created&pageSize=2&cursor={Uri.EscapeDataString(page1.NextCursor!)}",
            JsonOptions);
        Assert.NotNull(page2);
        Assert.Single(page2.Items);
        Assert.DoesNotContain(page2.Items, i => page1.Items.Any(p => p.Id == i.Id));

        var bList = await clientB.GetFromJsonAsync<AuditLogListResultDto>(
            "/api/v1/audit-logs?action=role.created&pageSize=50",
            JsonOptions);
        Assert.NotNull(bList);
        Assert.All(bList.Items, i => Assert.DoesNotContain(page1.Items, p => p.Id == i.Id));
        Assert.Contains(bList.Items, i => i.EntityDisplayName == "B Role" || i.NewValuesJson!.Contains("B Role"));
    }

    [Fact]
    public async Task Export_returns_csv_for_owner()
    {
        await factory.ResetDatabaseAsync();
        var owner = await RegisterOwnerAsync("Audit Export Co", "audit-export@example.com");
        var client = AuthedClient(owner.Token);
        await client.PostAsJsonAsync(
            "/api/v1/roles",
            new CreateRoleRequest("Export Role", null, null, [PermissionCodes.UsersRead]));

        var export = await client.GetAsync("/api/v1/audit-logs/export?action=role.created");
        Assert.Equal(HttpStatusCode.OK, export.StatusCode);
        Assert.Equal("text/csv", export.Content.Headers.ContentType?.MediaType);
        var bytes = await export.Content.ReadAsByteArrayAsync();
        var text = Encoding.UTF8.GetString(bytes);
        Assert.Contains("role.created", text, StringComparison.Ordinal);
        Assert.Contains("Export Role", text, StringComparison.Ordinal);
    }

    private async Task<(string Slug, string Email, Guid TenantId, string Token)> RegisterOwnerAsync(
        string companyName,
        string email)
    {
        var client = factory.CreateCookieClient();
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
