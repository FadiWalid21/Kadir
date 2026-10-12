using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kadr.Api.Endpoints;
using Kadr.Platform.Application.Employees;
using Kadr.Platform.Application.Lookups;
using Kadr.TenantIsolation.Tests.Auth;
using Kadr.TenantIsolation.Tests.Register;

namespace Kadr.TenantIsolation.Tests.Lookups;

[Collection(RegisterCollection.Name)]
public sealed class LookupApiTests(RegisterApiFactory factory)
{
    private static readonly JsonSerializerOptions JsonOptions = AuthTestHelper.JsonOptions;

    [Fact]
    public async Task Employee_lookup_is_isolated_and_matches_arabic_alef_folding()
    {
        await factory.ResetDatabaseAsync();
        var helper = new AuthTestHelper(factory);
        var a = await helper.RegisterVerifiedAsync("Lookup Co A", "lookup-a@example.com");
        var b = await helper.RegisterVerifiedAsync("Lookup Co B", "lookup-b@example.com");
        var clientA = helper.Authed(
            (await helper.LoginSessionAsync(a.Email, AuthTestHelper.Password, a.Slug)).AccessToken);
        var clientB = helper.Authed(
            (await helper.LoginSessionAsync(b.Email, AuthTestHelper.Password, b.Slug)).AccessToken);

        var create = await clientA.PostAsJsonAsync(
            "/api/v1/employees",
            new { fullNameAr = "أحمد علي", fullNameEn = "Ahmed Ali", hireDate = "2024-01-15", status = "Active" });
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var created = (await create.Content.ReadFromJsonAsync<EmployeeDetailDto>(JsonOptions))!;

        clientA.DefaultRequestHeaders.AcceptLanguage.Clear();
        clientA.DefaultRequestHeaders.AcceptLanguage.ParseAdd("ar");
        var listA = await clientA.GetFromJsonAsync<PagedLookupDto>(
            "/api/v1/lookups/employees?q=احمد&page=1&pageSize=20",
            JsonOptions);
        Assert.NotNull(listA);
        Assert.Contains(listA.Items, i => i.Id == created.Id);
        Assert.Equal("أحمد علي", listA.Items.First(i => i.Id == created.Id).Name);

        var listB = await clientB.GetFromJsonAsync<PagedLookupDto>(
            "/api/v1/lookups/employees?q=احمد&page=1&pageSize=20",
            JsonOptions);
        Assert.NotNull(listB);
        Assert.DoesNotContain(listB.Items, i => i.Id == created.Id);
        Assert.Equal(0, listB.TotalCount);
    }

    [Fact]
    public async Task Lookup_page_size_is_capped_at_100()
    {
        await factory.ResetDatabaseAsync();
        var helper = new AuthTestHelper(factory);
        var account = await helper.RegisterVerifiedAsync("Lookup Cap Co", "lookup-cap@example.com");
        var client = helper.Authed(
            (await helper.LoginSessionAsync(account.Email, AuthTestHelper.Password, account.Slug)).AccessToken);

        var response = await client.GetAsync("/api/v1/lookups/branches?page=1&pageSize=250");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<PagedLookupDto>(JsonOptions))!;
        Assert.Equal(100, body.PageSize);
        Assert.True(body.Items.Count <= 100);
    }

    [Fact]
    public async Task Lookup_employees_unauthorized_without_token()
    {
        await factory.ResetDatabaseAsync();
        var anon = factory.CreateClient();
        var response = await anon.GetAsync("/api/v1/lookups/employees");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Platform_tenant_lookup_requires_platform_admin()
    {
        await factory.ResetDatabaseAsync();
        var helper = new AuthTestHelper(factory);
        var account = await helper.RegisterVerifiedAsync("Lookup Tenant Co", "lookup-tenant@example.com");
        var tenantClient = helper.Authed(
            (await helper.LoginSessionAsync(account.Email, AuthTestHelper.Password, account.Slug)).AccessToken);
        var forbidden = await tenantClient.GetAsync("/api/v1/platform/lookups/tenants");
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        var platform = factory.CreateClient();
        var login = await platform.PostAsJsonAsync(
            "/api/v1/platform/auth/login",
            new PlatformLoginRequest(RegisterApiFactory.PlatformAdminEmail, RegisterApiFactory.PlatformAdminPassword));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var session = (await login.Content.ReadFromJsonAsync<JsonElement>(JsonOptions))!;
        var accessToken = session.GetProperty("accessToken").GetString()!;
        var admin = helper.Authed(accessToken);
        var ok = await admin.GetFromJsonAsync<PagedLookupDto>(
            $"/api/v1/platform/lookups/tenants?q={Uri.EscapeDataString(account.Slug)}&page=1&pageSize=20",
            JsonOptions);
        Assert.NotNull(ok);
        Assert.Contains(ok.Items, i => i.Name.Contains("Lookup Tenant", StringComparison.OrdinalIgnoreCase));
    }
}
