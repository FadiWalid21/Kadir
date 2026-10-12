using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Kadr.Api.Endpoints;
using Kadr.Identity.Application.Auth;
using Kadr.Identity.Domain.Roles;
using Kadr.Infrastructure.Persistence;
using Kadr.Platform.Application.Payroll;
using Kadr.SharedKernel.Constants;
using Kadr.SharedKernel.Tenancy;
using Kadr.TenantIsolation.Tests.Register;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Kadr.TenantIsolation.Tests.Payroll;

[Collection(RegisterCollection.Name)]
public sealed class PayComponentsApiTests(RegisterApiFactory factory)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private const string Password = "Str0ngPass!!";
    private const string BasePath = "/api/v1/payroll/pay-components";

    [Fact]
    public async Task List_seeds_basic_and_create_rejects_duplicate_key()
    {
        await factory.ResetDatabaseAsync();
        var session = await RegisterAndLoginAsync("Pay Co", "pay-create@example.com");

        var list = await session.Client.GetFromJsonAsync<PagedPayComponentsDto>(
            $"{BasePath}?page=1&pageSize=50",
            JsonOptions);
        Assert.NotNull(list);
        var basic = Assert.Single(list.Items);
        Assert.Equal("basic", basic.Key);
        Assert.True(basic.IsActive);
        Assert.Equal("Earning", basic.Kind);
        Assert.Equal("Fixed", basic.CalcType);

        var create = await session.Client.PostAsJsonAsync(
            BasePath,
            new CreatePayComponentRequest(
                "housing",
                "بدل سكن",
                "Housing",
                "Earning",
                "Fixed",
                IsTaxable: true,
                IsInsurable: false));
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<PayComponentDto>(JsonOptions);
        Assert.NotNull(created);
        Assert.Equal("housing", created.Key);

        var dup = await session.Client.PostAsJsonAsync(
            BasePath,
            new CreatePayComponentRequest(
                "housing",
                "أخرى",
                "Other",
                "Earning",
                "Fixed",
                IsTaxable: true,
                IsInsurable: false));
        Assert.Equal(HttpStatusCode.BadRequest, dup.StatusCode);
        Assert.Contains(ErrorCodes.SalaryDuplicateKey, await dup.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Deactivate_hides_from_default_list()
    {
        await factory.ResetDatabaseAsync();
        var session = await RegisterAndLoginAsync("Pay Deact Co", "pay-deact@example.com");

        var create = await session.Client.PostAsJsonAsync(
            BasePath,
            new CreatePayComponentRequest(
                "transport",
                "بدل مواصلات",
                "Transport",
                "Earning",
                "Fixed",
                IsTaxable: false,
                IsInsurable: false));
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<PayComponentDto>(JsonOptions);
        Assert.NotNull(created);

        var deactivate = await session.Client.PostAsync($"{BasePath}/{created.Id}/deactivate", null);
        Assert.Equal(HttpStatusCode.OK, deactivate.StatusCode);
        var deactivated = await deactivate.Content.ReadFromJsonAsync<PayComponentDto>(JsonOptions);
        Assert.False(deactivated!.IsActive);

        var activeList = await session.Client.GetFromJsonAsync<PagedPayComponentsDto>(
            $"{BasePath}?page=1&pageSize=50",
            JsonOptions);
        Assert.DoesNotContain(activeList!.Items, x => x.Id == created.Id);

        var allList = await session.Client.GetFromJsonAsync<PagedPayComponentsDto>(
            $"{BasePath}?page=1&pageSize=50&isActive=false",
            JsonOptions);
        Assert.Contains(allList!.Items, x => x.Id == created.Id && !x.IsActive);
    }

    [Fact]
    public async Task Pay_components_are_tenant_isolated_and_permission_gated()
    {
        await factory.ResetDatabaseAsync();
        var a = await RegisterAndLoginAsync("Iso Pay A", "pay-iso-a@example.com");
        var b = await RegisterAndLoginAsync("Iso Pay B", "pay-iso-b@example.com");

        var createA = await a.Client.PostAsJsonAsync(
            BasePath,
            new CreatePayComponentRequest(
                "bonus",
                "مكافأة",
                "Bonus",
                "Earning",
                "Fixed",
                IsTaxable: true,
                IsInsurable: false));
        Assert.Equal(HttpStatusCode.OK, createA.StatusCode);
        var componentA = await createA.Content.ReadFromJsonAsync<PayComponentDto>(JsonOptions);
        Assert.NotNull(componentA);

        var crossGet = await b.Client.GetAsync($"{BasePath}/{componentA.Id}");
        Assert.Equal(HttpStatusCode.NotFound, crossGet.StatusCode);

        var crossUpdate = await b.Client.PutAsJsonAsync(
            $"{BasePath}/{componentA.Id}",
            new UpdatePayComponentRequest(
                "قرصنة",
                "Hacked",
                "Deduction",
                "Fixed",
                IsTaxable: false,
                IsInsurable: false));
        Assert.Equal(HttpStatusCode.NotFound, crossUpdate.StatusCode);

        var listB = await b.Client.GetFromJsonAsync<PagedPayComponentsDto>(
            $"{BasePath}?page=1&pageSize=50",
            JsonOptions);
        Assert.DoesNotContain(listB!.Items, x => x.Id == componentA.Id);

        var setup = factory.CreateCookieClient();
        var reg = await RegisterVerifiedAsync(setup, "NoPerm Pay", "pay-noperm@example.com");
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

        var forbiddenRead = await emp.GetAsync(BasePath);
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenRead.StatusCode);

        var forbiddenWrite = await emp.PostAsJsonAsync(
            BasePath,
            new CreatePayComponentRequest(
                "x",
                "س",
                "X",
                "Earning",
                "Fixed",
                IsTaxable: true,
                IsInsurable: false));
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenWrite.StatusCode);
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
