using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Kadr.Api.Endpoints;
using Kadr.Infrastructure.Persistence;
using Kadr.Platform.Application.PlatformPlans;
using Kadr.SharedKernel.Constants;
using Kadr.TenantIsolation.Tests.Auth;
using Kadr.TenantIsolation.Tests.Register;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kadr.TenantIsolation.Tests.PlatformAdmin;

[Collection(RegisterCollection.Name)]
public sealed class PlatformPlansTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly RegisterApiFactory _factory;

    public PlatformPlansTests(RegisterApiFactory factory) => _factory = factory;

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Tenant_jwt_and_anonymous_cannot_reach_plan_endpoints()
    {
        var helper = new AuthTestHelper(_factory);
        var account = await helper.RegisterVerifiedAsync("Co A", "owner-a@example.com");
        var session = await helper.LoginSessionAsync(account.Email, AuthTestHelper.Password, account.Slug);
        var tenantClient = helper.Authed(session.AccessToken);
        var body = Request("Starter", "Starter", 25, 1499m, null, AllFeatures(true));

        Assert.Equal(HttpStatusCode.Forbidden, (await tenantClient.GetAsync("/api/v1/platform/plans")).StatusCode);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await tenantClient.PutAsJsonAsync($"/api/v1/platform/plans/{Guid.NewGuid()}", body)).StatusCode);

        var anonymous = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/v1/platform/plans")).StatusCode);
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await anonymous.PutAsJsonAsync($"/api/v1/platform/plans/{Guid.NewGuid()}", body)).StatusCode);
    }

    [Fact]
    public async Task Platform_admin_lists_all_seeded_plans_including_inactive()
    {
        var client = await PlatformClientAsync();

        var before = await ListAsync(client);
        Assert.Equal(
            new[] { PlanCodes.Business, PlanCodes.Core, PlanCodes.Growth, PlanCodes.Starter },
            before.Select(p => p.Code).Order().ToArray());
        Assert.All(before, p => Assert.True(p.IsActive));

        var growth = before.Single(p => p.Code == PlanCodes.Growth);
        var update = await client.PutAsJsonAsync(
            $"/api/v1/platform/plans/{growth.Id}",
            Request(growth.NameAr, growth.NameEn, growth.EmployeeCap, growth.UnitPrice, null, growth.FeaturesJson, false));
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        var after = await ListAsync(client);
        Assert.Equal(4, after.Count);
        Assert.False(after.Single(p => p.Code == PlanCodes.Growth).IsActive);
    }

    [Fact]
    public async Task Platform_admin_updates_a_flat_plan_and_features_are_normalized()
    {
        var client = await PlatformClientAsync();
        var starter = (await ListAsync(client)).Single(p => p.Code == PlanCodes.Starter);

        var response = await client.PutAsJsonAsync(
            $"/api/v1/platform/plans/{starter.Id}",
            Request("  المبتدئ بلس ", " Starter Plus ", 40, 1799.50m, null, "{\"attendance\":true,\"leave\":true}"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = (await response.Content.ReadFromJsonAsync<PlatformPlanDto>(JsonOptions))!;
        Assert.Equal(starter.Id, updated.Id);
        Assert.Equal(PlanCodes.Starter, updated.Code);
        Assert.Equal("FlatTier", updated.PricingModel);
        Assert.Equal("المبتدئ بلس", updated.NameAr);
        Assert.Equal("Starter Plus", updated.NameEn);
        Assert.Equal(40, updated.EmployeeCap);
        Assert.Equal(1799.50m, updated.UnitPrice);
        Assert.NotNull(updated.UpdatedAt);
        Assert.True(updated.Features[FeatureKeys.Attendance]);
        Assert.True(updated.Features[FeatureKeys.Leave]);
        Assert.False(updated.Features[FeatureKeys.Reports]);
        Assert.Equal(FeatureKeys.All.Count, updated.Features.Count);

        var persisted = (await ListAsync(client)).Single(p => p.Id == starter.Id);
        Assert.Equal(40, persisted.EmployeeCap);
        // jsonb does not preserve key order, so compare the parsed flags.
        Assert.Equal(updated.Features.OrderBy(f => f.Key), persisted.Features.OrderBy(f => f.Key));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KadrDbContext>();
        var adminId = await db.PlatformAdmins.AsNoTracking()
            .Where(a => a.Email == RegisterApiFactory.PlatformAdminEmail)
            .Select(a => a.Id)
            .SingleAsync();
        var row = await db.Plans.AsNoTracking().SingleAsync(p => p.Id == starter.Id);
        Assert.Equal(adminId, row.UpdatedBy);
    }

    [Fact]
    public async Task Platform_admin_updates_a_per_employee_plan()
    {
        var client = await PlatformClientAsync();
        var core = (await ListAsync(client)).Single(p => p.Code == PlanCodes.Core);
        Assert.Null(core.EmployeeCap);

        var response = await client.PutAsJsonAsync(
            $"/api/v1/platform/plans/{core.Id}",
            Request(core.NameAr, core.NameEn, null, 55m, 20, core.FeaturesJson));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = (await response.Content.ReadFromJsonAsync<PlatformPlanDto>(JsonOptions))!;
        Assert.Equal("PerEmployee", updated.PricingModel);
        Assert.Equal(55m, updated.UnitPrice);
        Assert.Equal(20, updated.MinBilledEmployees);
        Assert.Null(updated.EmployeeCap);
    }

    [Fact]
    public async Task Invalid_plan_updates_are_rejected_without_changing_the_plan()
    {
        var client = await PlatformClientAsync();
        var plans = await ListAsync(client);
        var starter = plans.Single(p => p.Code == PlanCodes.Starter);
        var core = plans.Single(p => p.Code == PlanCodes.Core);
        var flatUrl = $"/api/v1/platform/plans/{starter.Id}";
        var perEmployeeUrl = $"/api/v1/platform/plans/{core.Id}";

        var invalidBodies = new (string Url, PlatformUpdatePlanRequest Body)[]
        {
            (flatUrl, Request("", "Starter", 25, 1499m, null, starter.FeaturesJson)),
            (flatUrl, Request("المبتدئ", "Starter", 0, 1499m, null, starter.FeaturesJson)),
            (flatUrl, Request("المبتدئ", "Starter", 25, -1m, null, starter.FeaturesJson)),
            (flatUrl, Request("المبتدئ", "Starter", 25, 10.123m, null, starter.FeaturesJson)),
            (flatUrl, Request("المبتدئ", "Starter", 25, 1499m, null, "not json")),
            (flatUrl, Request("المبتدئ", "Starter", 25, 1499m, null, "{\"made_up\":true}")),
            (flatUrl, Request("المبتدئ", "Starter", 25, 1499m, null, "{\"attendance\":\"yes\"}")),
            // Flat tiers need a cap and a price, and have no minimum billed headcount.
            (flatUrl, Request("المبتدئ", "Starter", null, 1499m, null, starter.FeaturesJson)),
            (flatUrl, Request("المبتدئ", "Starter", 25, 1499m, 5, starter.FeaturesJson)),
            // Per-employee plans need a price and a minimum billed headcount.
            (perEmployeeUrl, Request("الأساسي", "Core", null, null, 10, core.FeaturesJson)),
            (perEmployeeUrl, Request("الأساسي", "Core", null, 49m, null, core.FeaturesJson)),
        };

        foreach (var (url, body) in invalidBodies)
        {
            var response = await client.PutAsJsonAsync(url, body);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(ErrorCodes.PlatformValidationFailed, await AuthTestHelper.ReadErrorCodeAsync(response));
        }

        var after = await ListAsync(client);
        Assert.Equal(starter.NameEn, after.Single(p => p.Code == PlanCodes.Starter).NameEn);
        Assert.Equal(starter.UnitPrice, after.Single(p => p.Code == PlanCodes.Starter).UnitPrice);
        Assert.Equal(core.UnitPrice, after.Single(p => p.Code == PlanCodes.Core).UnitPrice);
    }

    [Fact]
    public async Task Unknown_plan_is_not_found_and_trial_default_cannot_be_deactivated()
    {
        var client = await PlatformClientAsync();
        var starter = (await ListAsync(client)).Single(p => p.Code == PlanCodes.TrialDefault);

        var missing = await client.PutAsJsonAsync(
            $"/api/v1/platform/plans/{Guid.NewGuid()}",
            Request("اسم", "Name", 10, 10m, null, starter.FeaturesJson));
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal(ErrorCodes.PlatformPlanNotFound, await AuthTestHelper.ReadErrorCodeAsync(missing));

        var deactivate = await client.PutAsJsonAsync(
            $"/api/v1/platform/plans/{starter.Id}",
            Request(starter.NameAr, starter.NameEn, starter.EmployeeCap, starter.UnitPrice, null, starter.FeaturesJson, false));
        Assert.Equal(HttpStatusCode.Conflict, deactivate.StatusCode);
        Assert.Equal(ErrorCodes.PlatformPlanProtected, await AuthTestHelper.ReadErrorCodeAsync(deactivate));
        Assert.True((await ListAsync(client)).Single(p => p.Id == starter.Id).IsActive);
    }

    [Fact]
    public async Task Inactive_plan_cannot_be_chosen_in_tenant_change_plan()
    {
        var helper = new AuthTestHelper(_factory);
        var account = await helper.RegisterVerifiedAsync("Co A", "owner-a@example.com");
        var client = await PlatformClientAsync();
        var growth = (await ListAsync(client)).Single(p => p.Code == PlanCodes.Growth);

        var deactivate = await client.PutAsJsonAsync(
            $"/api/v1/platform/plans/{growth.Id}",
            Request(growth.NameAr, growth.NameEn, growth.EmployeeCap, growth.UnitPrice, null, growth.FeaturesJson, false));
        Assert.Equal(HttpStatusCode.OK, deactivate.StatusCode);

        var change = await client.PostAsJsonAsync(
            $"/api/v1/platform/tenants/{account.TenantId}/change-plan",
            new Kadr.Platform.Application.PlatformTenants.PlatformChangePlanRequest(PlanCodes.Growth, "Check"));
        Assert.Equal(HttpStatusCode.BadRequest, change.StatusCode);
        Assert.Equal(ErrorCodes.PlanNotFound, await AuthTestHelper.ReadErrorCodeAsync(change));
    }

    private static PlatformUpdatePlanRequest Request(
        string nameAr,
        string nameEn,
        int? employeeCap,
        decimal? unitPrice,
        int? minBilled,
        string featuresJson,
        bool isActive = true) =>
        new(nameAr, nameEn, employeeCap, unitPrice, minBilled, featuresJson, isActive);

    private static string AllFeatures(bool value) =>
        JsonSerializer.Serialize(FeatureKeys.All.ToDictionary(k => k, _ => value));

    private async Task<HttpClient> PlatformClientAsync()
    {
        var client = _factory.CreateCookieClient();
        var login = await client.PostAsJsonAsync(
            "/api/v1/platform/auth/login",
            new PlatformLoginRequest(RegisterApiFactory.PlatformAdminEmail, RegisterApiFactory.PlatformAdminPassword));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var tokens = (await login.Content.ReadFromJsonAsync<AccessTokenResponse>(JsonOptions))!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        return client;
    }

    private static async Task<IReadOnlyList<PlatformPlanDto>> ListAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/v1/platform/plans");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<PlatformPlanListResultDto>(JsonOptions))!;
        return body.Items;
    }
}
