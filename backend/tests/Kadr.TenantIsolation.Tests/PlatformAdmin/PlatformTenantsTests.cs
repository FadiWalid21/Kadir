using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Kadr.Api.Endpoints;
using Kadr.Platform.Application.PlatformTenants;
using Kadr.SharedKernel.Constants;
using Kadr.TenantIsolation.Tests.Auth;
using Kadr.TenantIsolation.Tests.Register;
using Microsoft.EntityFrameworkCore;

namespace Kadr.TenantIsolation.Tests.PlatformAdmin;

[Collection(RegisterCollection.Name)]
public sealed class PlatformTenantsTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly RegisterApiFactory _factory;

    public PlatformTenantsTests(RegisterApiFactory factory) => _factory = factory;

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Tenant_jwt_is_forbidden_on_platform_tenant_list()
    {
        var helper = new AuthTestHelper(_factory);
        var account = await helper.RegisterVerifiedAsync("Co A", "owner-a@example.com");
        var session = await helper.LoginSessionAsync(account.Email, AuthTestHelper.Password, account.Slug);

        var client = helper.Authed(session.AccessToken);
        var list = await client.GetAsync("/api/v1/platform/tenants");
        var detail = await client.GetAsync($"/api/v1/platform/tenants/{account.TenantId}");
        var suspend = await client.PostAsJsonAsync(
            $"/api/v1/platform/tenants/{account.TenantId}/suspend",
            new PlatformReasonRequest("not allowed"));

        Assert.Equal(HttpStatusCode.Forbidden, list.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, detail.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, suspend.StatusCode);
    }

    [Fact]
    public async Task Anonymous_is_unauthorized_on_platform_tenant_list()
    {
        var response = await _factory.CreateClient().GetAsync("/api/v1/platform/tenants");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Platform_admin_can_list_and_filter_tenants()
    {
        var helper = new AuthTestHelper(_factory);
        var a = await helper.RegisterVerifiedAsync("Alpha Trading", "owner-a@example.com");
        await helper.RegisterVerifiedAsync("Beta Clinic", "owner-b@example.com");
        var client = await PlatformClientAsync();

        var all = await GetAsync<PlatformTenantListResultDto>(client, "/api/v1/platform/tenants");
        Assert.Equal(2, all.TotalCount);
        Assert.Equal(2, all.Summary.Total);
        Assert.Equal(2, all.Summary.Trial);
        Assert.Equal(0, all.Summary.Suspended);
        Assert.All(all.Items, i => Assert.Equal(1, i.UserCount));

        var search = await GetAsync<PlatformTenantListResultDto>(client, "/api/v1/platform/tenants?q=alpha");
        var only = Assert.Single(search.Items);
        Assert.Equal(a.TenantId, only.Id);

        var none = await GetAsync<PlatformTenantListResultDto>(client, "/api/v1/platform/tenants?status=Suspended");
        Assert.Empty(none.Items);
        Assert.Equal(2, none.Summary.Total);

        var badStatus = await client.GetAsync("/api/v1/platform/tenants?status=bogus");
        Assert.Equal(HttpStatusCode.BadRequest, badStatus.StatusCode);

        var capped = await GetAsync<PlatformTenantListResultDto>(client, "/api/v1/platform/tenants?pageSize=1000");
        Assert.Equal(100, capped.PageSize);
    }

    [Fact]
    public async Task Platform_admin_can_read_tenant_detail_with_owner_and_usage()
    {
        var helper = new AuthTestHelper(_factory);
        var account = await helper.RegisterVerifiedAsync("Co A", "owner-a@example.com");
        var client = await PlatformClientAsync();

        var detail = await GetAsync<PlatformTenantDetailDto>(client, $"/api/v1/platform/tenants/{account.TenantId}");

        Assert.Equal("Co A", detail.Company.Name);
        Assert.Equal("Trial", detail.Company.Status);
        Assert.NotNull(detail.Owner);
        Assert.Equal("owner-a@example.com", detail.Owner.Email);
        Assert.Equal(1, detail.Usage.UserCount);
        Assert.Equal(0, detail.Usage.ActiveEmployeeCount);
        Assert.NotNull(detail.Subscription);

        var missing = await client.GetAsync($"/api/v1/platform/tenants/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task Change_industry_requires_a_reason()
    {
        var helper = new AuthTestHelper(_factory);
        var account = await helper.RegisterVerifiedAsync("Co A", "owner-a@example.com");
        var client = await PlatformClientAsync();

        var response = await client.PostAsJsonAsync(
            $"/api/v1/platform/tenants/{account.TenantId}/change-industry",
            new PlatformChangeIndustryRequest("retail", TemplateSyncModes.AddMissing, "  "));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(ErrorCodes.PlatformReasonRequired, await AuthTestHelper.ReadErrorCodeAsync(response));

        var detail = await GetAsync<PlatformTenantDetailDto>(client, $"/api/v1/platform/tenants/{account.TenantId}");
        Assert.Equal("office", detail.Company.IndustryCode);
    }

    [Fact]
    public async Task Suspend_marks_tenant_suspended_audits_and_resume_restores_it()
    {
        var helper = new AuthTestHelper(_factory);
        var account = await helper.RegisterVerifiedAsync("Co A", "owner-a@example.com");
        var client = await PlatformClientAsync();
        var url = $"/api/v1/platform/tenants/{account.TenantId}";

        var noReason = await client.PostAsJsonAsync($"{url}/suspend", new PlatformReasonRequest(""));
        Assert.Equal(HttpStatusCode.BadRequest, noReason.StatusCode);

        var suspend = await client.PostAsJsonAsync($"{url}/suspend", new PlatformReasonRequest("Unpaid invoice"));
        Assert.Equal(HttpStatusCode.OK, suspend.StatusCode);
        var suspended = (await suspend.Content.ReadFromJsonAsync<PlatformTenantDetailDto>(JsonOptions))!;
        Assert.Equal("Suspended", suspended.Company.Status);
        Assert.Equal("Suspended", suspended.Subscription!.Status);
        Assert.True(suspended.Subscription.ReadOnly);

        var again = await client.PostAsJsonAsync($"{url}/suspend", new PlatformReasonRequest("Twice"));
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal(ErrorCodes.PlatformTenantStateInvalid, await AuthTestHelper.ReadErrorCodeAsync(again));

        Assert.Equal(1, await helper.CountAuditAsync(account.TenantId, PlatformAuditActions.TenantSuspended));

        var summary = await GetAsync<PlatformTenantAuditSummaryDto>(client, $"{url}/audit-summary");
        var action = Assert.Single(summary.PlatformActions);
        Assert.Equal(PlatformAuditActions.TenantSuspended, action.Action);
        Assert.Equal("Unpaid invoice", action.Reason);
        Assert.Equal(RegisterApiFactory.PlatformAdminEmail, action.AdminEmail);
        Assert.DoesNotContain(summary.RecentTenantEvents, e => e.Action.StartsWith("platform.", StringComparison.Ordinal));

        var resume = await client.PostAsJsonAsync($"{url}/resume", new PlatformReasonRequest("Paid"));
        Assert.Equal(HttpStatusCode.OK, resume.StatusCode);
        var resumed = (await resume.Content.ReadFromJsonAsync<PlatformTenantDetailDto>(JsonOptions))!;
        Assert.Equal("Trial", resumed.Company.Status);
        Assert.Equal("Trialing", resumed.Subscription!.Status);
        Assert.False(resumed.Subscription.ReadOnly);

        var resumeAgain = await client.PostAsJsonAsync($"{url}/resume", new PlatformReasonRequest("Again"));
        Assert.Equal(HttpStatusCode.Conflict, resumeAgain.StatusCode);
    }

    [Fact]
    public async Task Extend_trial_accepts_only_allowed_days_and_moves_the_end_date()
    {
        var helper = new AuthTestHelper(_factory);
        var account = await helper.RegisterVerifiedAsync("Co A", "owner-a@example.com");
        var client = await PlatformClientAsync();
        var url = $"/api/v1/platform/tenants/{account.TenantId}";
        var before = await GetAsync<PlatformTenantDetailDto>(client, url);

        var invalid = await client.PostAsJsonAsync($"{url}/extend-trial", new PlatformExtendTrialRequest(10, "x"));
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);

        var ok = await client.PostAsJsonAsync($"{url}/extend-trial", new PlatformExtendTrialRequest(14, "Sales request"));
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        var after = (await ok.Content.ReadFromJsonAsync<PlatformTenantDetailDto>(JsonOptions))!;

        Assert.Equal(before.Subscription!.TrialEndsAt!.Value.AddDays(14), after.Subscription!.TrialEndsAt!.Value);
        Assert.Equal(1, await helper.CountAuditAsync(account.TenantId, PlatformAuditActions.TrialExtended));
    }

    [Fact]
    public async Task Change_plan_and_cancel_subscription_are_audited()
    {
        var helper = new AuthTestHelper(_factory);
        var account = await helper.RegisterVerifiedAsync("Co A", "owner-a@example.com");
        var client = await PlatformClientAsync();
        var url = $"/api/v1/platform/tenants/{account.TenantId}";
        var before = await GetAsync<PlatformTenantDetailDto>(client, url);

        var unknown = await client.PostAsJsonAsync(
            $"{url}/change-plan", new PlatformChangePlanRequest("no-such-plan", "x"));
        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
        Assert.Equal(ErrorCodes.PlanNotFound, await AuthTestHelper.ReadErrorCodeAsync(unknown));

        var same = await client.PostAsJsonAsync(
            $"{url}/change-plan", new PlatformChangePlanRequest(before.Subscription!.PlanCode, "x"));
        Assert.Equal(HttpStatusCode.Conflict, same.StatusCode);

        var cancel = await client.PostAsJsonAsync($"{url}/cancel-subscription", new PlatformReasonRequest("Churned"));
        Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);
        var cancelled = (await cancel.Content.ReadFromJsonAsync<PlatformTenantDetailDto>(JsonOptions))!;
        Assert.Equal("Cancelled", cancelled.Subscription!.Status);
        Assert.Equal(1, await helper.CountAuditAsync(account.TenantId, PlatformAuditActions.SubscriptionCancelled));

        var cancelAgain = await client.PostAsJsonAsync($"{url}/cancel-subscription", new PlatformReasonRequest("Twice"));
        Assert.Equal(HttpStatusCode.Conflict, cancelAgain.StatusCode);
    }

    [Fact]
    public async Task Change_industry_replace_untouched_deactivates_unmodified_items_and_keeps_customized_ones()
    {
        var helper = new AuthTestHelper(_factory);
        var account = await helper.RegisterVerifiedAsync("Co A", "owner-a@example.com");
        var client = await PlatformClientAsync();
        var url = $"/api/v1/platform/tenants/{account.TenantId}";

        // Customize one office-only job title so it counts as touched.
        await helper.WithTenantAsync(account.TenantId, async db =>
        {
            var job = await db.JobTitles.SingleAsync(j => j.SourceKey == "job.manager");
            job.Update("مدير مخصص", "Custom manager");
            await db.SaveChangesAsync();
            return 0;
        });

        var response = await client.PostAsJsonAsync(
            $"{url}/change-industry",
            new PlatformChangeIndustryRequest("retail", TemplateSyncModes.ReplaceUntouched, "Misclassified at signup"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<PlatformChangeIndustryResultDto>(JsonOptions))!;
        Assert.Equal("office", body.OldIndustryCode);
        Assert.Equal("retail", body.NewIndustryCode);
        Assert.True(body.AddedCount > 0);
        Assert.True(body.DeactivatedCount > 0);
        Assert.Equal("retail", body.Tenant.Company.IndustryCode);

        await helper.WithTenantAsync(account.TenantId, async db =>
        {
            var custom = await db.JobTitles.SingleAsync(j => j.SourceKey == "job.manager");
            Assert.True(custom.IsActive);
            Assert.True(custom.IsCustomized);

            var untouchedOldOnly = await db.JobTitles.SingleAsync(j => j.SourceKey == "job.accountant");
            Assert.False(untouchedOldOnly.IsActive);

            var shared = await db.JobTitles.SingleAsync(j => j.SourceKey == "job.hr");
            Assert.True(shared.IsActive);

            Assert.True(await db.JobTitles.AnyAsync(j => j.SourceKey == "job.cashier" && j.IsActive));
            return 0;
        });

        Assert.Equal(1, await helper.CountAuditAsync(account.TenantId, PlatformAuditActions.IndustryChanged));

        var same = await client.PostAsJsonAsync(
            $"{url}/change-industry",
            new PlatformChangeIndustryRequest("retail", TemplateSyncModes.AddMissing, "Again"));
        Assert.Equal(HttpStatusCode.Conflict, same.StatusCode);
    }

    [Fact]
    public async Task Change_industry_add_missing_keeps_existing_items_active()
    {
        var helper = new AuthTestHelper(_factory);
        var account = await helper.RegisterVerifiedAsync("Co A", "owner-a@example.com");
        var client = await PlatformClientAsync();

        var response = await client.PostAsJsonAsync(
            $"/api/v1/platform/tenants/{account.TenantId}/change-industry",
            new PlatformChangeIndustryRequest("retail", TemplateSyncModes.AddMissing, "Needs retail items"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<PlatformChangeIndustryResultDto>(JsonOptions))!;
        Assert.True(body.AddedCount > 0);
        Assert.Equal(0, body.DeactivatedCount);

        await helper.WithTenantAsync(account.TenantId, async db =>
        {
            Assert.Equal(0, await db.JobTitles.CountAsync(j => !j.IsActive));
            return 0;
        });
    }

    [Fact]
    public async Task Platform_mutations_never_leave_a_tenant_context_on_the_request()
    {
        var helper = new AuthTestHelper(_factory);
        var account = await helper.RegisterVerifiedAsync("Co A", "owner-a@example.com");
        var client = await PlatformClientAsync();

        var suspend = await client.PostAsJsonAsync(
            $"/api/v1/platform/tenants/{account.TenantId}/suspend",
            new PlatformReasonRequest("Check scope"));
        Assert.Equal(HttpStatusCode.OK, suspend.StatusCode);

        // Same token keeps working as a platform admin afterwards.
        var me = await client.GetAsync("/api/v1/platform/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
    }

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

    private static async Task<T> GetAsync<T>(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<T>(JsonOptions))!;
    }
}
