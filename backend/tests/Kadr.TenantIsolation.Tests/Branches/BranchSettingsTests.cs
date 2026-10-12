using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Kadr.Api.Endpoints;
using Kadr.Identity.Application.Auth;
using Kadr.Identity.Domain.Roles;
using Kadr.Infrastructure.Persistence;
using Kadr.Platform.Application.Branches;
using Kadr.SharedKernel.Constants;
using Kadr.SharedKernel.Tenancy;
using Kadr.TenantIsolation.Tests.Register;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Kadr.TenantIsolation.Tests.Branches;

[Collection(RegisterCollection.Name)]
public sealed class BranchSettingsTests(RegisterApiFactory factory)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private const string Password = "Str0ngPass!!";

    [Fact]
    public async Task Create_branch_and_reject_duplicate_name()
    {
        await factory.ResetDatabaseAsync();
        var session = await RegisterAndLoginAsync("Branch Co", "branch-create@example.com");
        var legalId = await GetDefaultLegalEntityIdAsync(session.TenantId);

        var create = await session.Client.PostAsJsonAsync(
            "/api/v1/settings/branches",
            new CreateBranchRequest(legalId, "فرع ثانٍ", "Second Branch", "Cairo"));
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<BranchListItemDto>(JsonOptions);
        Assert.NotNull(created);
        Assert.Equal("Second Branch", created.NameEn);
        Assert.True(created.IsActive);

        var dup = await session.Client.PostAsJsonAsync(
            "/api/v1/settings/branches",
            new CreateBranchRequest(legalId, "فرع ثانٍ", "Other", null));
        Assert.Equal(HttpStatusCode.BadRequest, dup.StatusCode);
        Assert.Contains(ErrorCodes.BranchesDuplicateName, await dup.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Cannot_deactivate_last_active_branch()
    {
        await factory.ResetDatabaseAsync();
        var session = await RegisterAndLoginAsync("Last Active Co", "branch-last@example.com");
        var list = await session.Client.GetFromJsonAsync<PagedBranchesDto>(
            "/api/v1/settings/branches?page=1&pageSize=50",
            JsonOptions);
        var only = Assert.Single(list!.Items);

        var deactivate = await session.Client.PatchAsJsonAsync(
            $"/api/v1/settings/branches/{only.Id}/active",
            new SetBranchActiveRequest(false));
        Assert.Equal(HttpStatusCode.BadRequest, deactivate.StatusCode);
        Assert.Contains(
            ErrorCodes.BranchesLastActive,
            await deactivate.Content.ReadAsStringAsync(),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Branches_are_tenant_isolated_and_permission_gated()
    {
        await factory.ResetDatabaseAsync();
        var a = await RegisterAndLoginAsync("Iso Branch A", "branch-iso-a@example.com");
        var b = await RegisterAndLoginAsync("Iso Branch B", "branch-iso-b@example.com");
        var listA = await a.Client.GetFromJsonAsync<PagedBranchesDto>(
            "/api/v1/settings/branches?page=1&pageSize=50",
            JsonOptions);
        var id = listA!.Items[0].Id;

        var cross = await b.Client.PatchAsJsonAsync(
            $"/api/v1/settings/branches/{id}/active",
            new SetBranchActiveRequest(true));
        Assert.Equal(HttpStatusCode.NotFound, cross.StatusCode);

        var setup = factory.CreateCookieClient();
        var reg = await RegisterVerifiedAsync(setup, "NoPerm Branch", "branch-noperm@example.com");
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KadrDbContext>();
            var ambient = scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();
            ambient.Set(reg.TenantId);
            var user = await db.Users.SingleAsync(u => u.Email == reg.Email);
            var employeeRole = await db.Roles.SingleAsync(r =>
                r.TenantId == reg.TenantId && r.Name == SystemRoleNames.Employee);
            var ownerLink = await db.UserRoles.SingleAsync(ur => ur.UserId == user.Id);
            db.UserRoles.Remove(ownerLink);
            db.UserRoles.Add(UserRole.Create(reg.TenantId, user.Id, employeeRole.Id));
            await db.SaveChangesAsync();
            scope.ServiceProvider.GetRequiredService<IPermissionCacheInvalidator>().Invalidate(reg.TenantId, user.Id);
        }

        var empToken = await LoginAccessTokenAsync(factory.CreateCookieClient(), reg.Email, reg.Slug);
        var emp = factory.CreateCookieClient();
        emp.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", empToken);
        var forbidden = await emp.GetAsync("/api/v1/settings/branches");
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }

    private async Task<Guid> GetDefaultLegalEntityIdAsync(Guid tenantId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KadrDbContext>();
        var ambient = scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();
        ambient.Set(tenantId);
        return await db.LegalEntities.AsNoTracking().Select(x => x.Id).SingleAsync();
    }

    private async Task<(HttpClient Client, Guid TenantId)> RegisterAndLoginAsync(string company, string email)
    {
        var client = factory.CreateCookieClient();
        var (slug, _, tenantId) = await RegisterVerifiedAsync(client, company, email);
        var token = await LoginAccessTokenAsync(client, email, slug);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return (client, tenantId);
    }

    private async Task<(string Slug, string Email, Guid TenantId)> RegisterVerifiedAsync(
        HttpClient client,
        string companyName,
        string email)
    {
        factory.Emails.Clear();
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
        return (reg.Slug, email, reg.TenantId);
    }

    private static async Task<string> LoginAccessTokenAsync(HttpClient client, string email, string slug)
    {
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, Password, slug));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var body = await login.Content.ReadFromJsonAsync<AccessTokenResponse>(JsonOptions);
        return body!.AccessToken;
    }
}
