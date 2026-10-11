using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Kadr.Api.Endpoints;
using Kadr.Identity.Application.Auth;
using Kadr.Identity.Domain.Roles;
using Kadr.Infrastructure.Persistence;
using Kadr.Platform.Application.Abstractions;
using Kadr.Platform.Application.Subscriptions;
using Kadr.Platform.Domain.Subscriptions;
using Kadr.SharedKernel.Constants;
using Kadr.SharedKernel.Tenancy;
using Kadr.TenantIsolation.Tests.Register;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Kadr.TenantIsolation.Tests.Subscriptions;

[Collection(RegisterCollection.Name)]
public sealed class SubscriptionApiTests(RegisterApiFactory factory)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private const string Password = "Str0ngPass!!";

    [Fact]
    public async Task Ac17_flat_tier_cap_25_blocks_26()
    {
        await factory.ResetDatabaseAsync();
        var owner = await RegisterOwnerAsync("Cap Co", "cap@example.com");

        using var scope = factory.Services.CreateScope();
        var ambient = scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();
        ambient.Set(owner.TenantId, Guid.NewGuid());
        var guard = scope.ServiceProvider.GetRequiredService<ISubscriptionGuard>();

        var over = await guard.EnsureEmployeeCapacityAsync(26);
        Assert.True(over.IsFailure);
        Assert.Equal(ErrorCodes.PlanEmployeeCapExceeded, over.ErrorCode);

        var ok = await guard.EnsureEmployeeCapacityAsync(25);
        Assert.True(ok.IsSuccess);
    }

    [Fact]
    public async Task Ac18_require_feature_returns_plan_feature_not_available()
    {
        await factory.ResetDatabaseAsync();
        var owner = await RegisterOwnerAsync("Feat Co", "feat@example.com");
        var client = AuthedClient(owner.Token);

        var response = await client.PostAsync("/api/v1/dev/feature-check", null);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains(ErrorCodes.PlanFeatureNotAvailable, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Ac19_expired_trial_blocks_mutations_allows_reads_and_auth()
    {
        await factory.ResetDatabaseAsync();
        var owner = await RegisterOwnerAsync("Expire Co", "expire@example.com");
        var client = AuthedClient(owner.Token);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KadrDbContext>();
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE subscriptions SET trial_ends_at = {DateTimeOffset.UtcNow.AddDays(-2)}, renews_at = {DateTimeOffset.UtcNow.AddDays(-2)}, status = 0 WHERE tenant_id = {owner.TenantId}");
            scope.ServiceProvider.GetRequiredService<ISubscriptionCache>().Invalidate(owner.TenantId);
        }

        var getSub = await client.GetAsync("/api/v1/subscription");
        Assert.Equal(HttpStatusCode.OK, getSub.StatusCode);
        var dto = await getSub.Content.ReadFromJsonAsync<SubscriptionDto>(JsonOptions);
        Assert.NotNull(dto);
        Assert.True(dto.ReadOnly);
        Assert.Equal(SubscriptionStates.TrialExpired, dto.EffectiveState);

        var mutate = await client.PostAsJsonAsync(
            "/api/v1/roles",
            new Kadr.Platform.Application.Roles.CreateRoleRequest("X", null, null, [PermissionCodes.UsersRead]));
        Assert.Equal(HttpStatusCode.Forbidden, mutate.StatusCode);
        Assert.Contains(ErrorCodes.SubscriptionExpired, await mutate.Content.ReadAsStringAsync());

        var loginClient = factory.CreateCookieClient();
        var login = await loginClient.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest(owner.Email, Password, owner.Slug));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.DoesNotContain(ErrorCodes.SubscriptionExpired, await login.Content.ReadAsStringAsync());

        var refresh = await loginClient.PostAsync("/api/v1/auth/refresh", null);
        Assert.NotEqual(HttpStatusCode.Forbidden, refresh.StatusCode);
        Assert.DoesNotContain(ErrorCodes.SubscriptionExpired, await refresh.Content.ReadAsStringAsync());

        var logout = await loginClient.PostAsync("/api/v1/auth/logout", null);
        Assert.NotEqual(HttpStatusCode.Forbidden, logout.StatusCode);
        Assert.DoesNotContain(ErrorCodes.SubscriptionExpired, await logout.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Effective_state_computed_without_job()
    {
        await factory.ResetDatabaseAsync();
        var owner = await RegisterOwnerAsync("Compute Co", "compute@example.com");

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KadrDbContext>();
        var ambient = scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();
        ambient.Set(owner.TenantId);
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE subscriptions SET trial_ends_at = {DateTimeOffset.UtcNow.AddDays(-3)}, renews_at = {DateTimeOffset.UtcNow.AddDays(-3)}, status = 0 WHERE tenant_id = {owner.TenantId}");
        scope.ServiceProvider.GetRequiredService<ISubscriptionCache>().Invalidate(owner.TenantId);

        var guard = scope.ServiceProvider.GetRequiredService<ISubscriptionGuard>();
        var snap = await guard.GetSnapshotAsync();
        Assert.True(snap.IsSuccess);
        Assert.Equal(SubscriptionStates.TrialExpired, snap.Value!.EffectiveState);
        Assert.True(snap.Value.ReadOnly);
        Assert.Equal(SubscriptionStatus.Trialing.ToString(), snap.Value.PersistedStatus);
    }

    [Fact]
    public async Task Trial_job_is_idempotent_and_tenant_scoped()
    {
        await factory.ResetDatabaseAsync();
        var expired = await RegisterOwnerAsync("Job Expire Co", "job-expire@example.com");
        var active = await RegisterOwnerAsync("Job Active Co", "job-active@example.com");

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KadrDbContext>();
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE subscriptions SET trial_ends_at = {DateTimeOffset.UtcNow.AddDays(-1)}, renews_at = {DateTimeOffset.UtcNow.AddDays(-1)}, status = 0 WHERE tenant_id = {expired.TenantId}");
        }

        var runner = factory.Services.GetRequiredService<ITrialExpiryRunner>();
        await runner.RunOnceAsync();
        await runner.RunOnceAsync();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KadrDbContext>();
            var ambient = scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();

            ambient.Set(expired.TenantId);
            var expiredSub = await db.Subscriptions.IgnoreQueryFilters()
                .SingleAsync(s => s.TenantId == expired.TenantId);
            Assert.Equal(SubscriptionStatus.Suspended, expiredSub.Status);

            var audits = await db.AuditLogs.IgnoreQueryFilters()
                .CountAsync(a => a.TenantId == expired.TenantId && a.Action == "subscription.trial_expired");
            Assert.Equal(1, audits);

            ambient.Set(active.TenantId);
            var activeSub = await db.Subscriptions.IgnoreQueryFilters()
                .SingleAsync(s => s.TenantId == active.TenantId);
            Assert.Equal(SubscriptionStatus.Trialing, activeSub.Status);
        }
    }

    [Fact]
    public async Task Get_subscription_and_plans_require_permission_and_isolate_tenants()
    {
        await factory.ResetDatabaseAsync();
        var a = await RegisterOwnerAsync("Sub Iso A", "sub-iso-a@example.com");
        var b = await RegisterOwnerAsync("Sub Iso B", "sub-iso-b@example.com");
        await DemoteToEmployeeAsync(a.TenantId, a.Email);
        var empToken = await LoginAccessTokenAsync(factory.CreateCookieClient(), a.Email, a.Slug);
        var emp = AuthedClient(empToken);

        var forbidden = await emp.GetAsync("/api/v1/subscription");
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        var clientA = AuthedClient(a.Token);
        // a.Token is owner token from before demote — still has owner JWT claims until re-login
        // Use b for isolation: change A's plan via DB, B should still see starter
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KadrDbContext>();
            var ambient = scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();
            ambient.Set(a.TenantId);
            var growth = await db.Plans.AsNoTracking().SingleAsync(p => p.Code == PlanCodes.Growth);
            var sub = await db.Subscriptions.SingleAsync(s => s.TenantId == a.TenantId);
            sub.ChangePlan(growth.Id);
            await db.SaveChangesAsync();
            scope.ServiceProvider.GetRequiredService<ISubscriptionCache>().Invalidate(a.TenantId);
        }

        // Re-login owner A — need owner permissions. Create fresh owner session for A via DB restore? Use B vs A with owner tokens from registration before demote.
        // Register a third owner for permission-positive path.
        var reader = await RegisterOwnerAsync("Sub Reader", "sub-reader@example.com");
        var readerClient = AuthedClient(reader.Token);
        var subOk = await readerClient.GetAsync("/api/v1/subscription");
        Assert.Equal(HttpStatusCode.OK, subOk.StatusCode);
        var plansOk = await readerClient.GetAsync("/api/v1/plans");
        Assert.Equal(HttpStatusCode.OK, plansOk.StatusCode);

        var clientB = AuthedClient(b.Token);
        var subB = await clientB.GetFromJsonAsync<SubscriptionDto>("/api/v1/subscription", JsonOptions);
        Assert.Equal(PlanCodes.Starter, subB!.Plan.Code);

        // Owner A token still works for isolation (permissions in JWT are not used — permission service loads from DB)
        // After demote, a.Token may fail authorization — register separate tenants for isolation only:
        var isoA = await RegisterOwnerAsync("IsoA2", "iso-a2@example.com");
        var isoB = await RegisterOwnerAsync("IsoB2", "iso-b2@example.com");
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KadrDbContext>();
            var ambient = scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();
            ambient.Set(isoA.TenantId);
            var growth = await db.Plans.AsNoTracking().SingleAsync(p => p.Code == PlanCodes.Growth);
            var sub = await db.Subscriptions.SingleAsync(s => s.TenantId == isoA.TenantId);
            sub.ChangePlan(growth.Id);
            await db.SaveChangesAsync();
            scope.ServiceProvider.GetRequiredService<ISubscriptionCache>().Invalidate(isoA.TenantId);
        }

        var aDto = await AuthedClient(isoA.Token).GetFromJsonAsync<SubscriptionDto>("/api/v1/subscription", JsonOptions);
        var bDto = await AuthedClient(isoB.Token).GetFromJsonAsync<SubscriptionDto>("/api/v1/subscription", JsonOptions);
        Assert.Equal(PlanCodes.Growth, aDto!.Plan.Code);
        Assert.Equal(PlanCodes.Starter, bDto!.Plan.Code);
    }

    [Fact]
    public async Task Registration_creates_starter_trial_plus_14_days_with_features()
    {
        await factory.ResetDatabaseAsync();
        var owner = await RegisterOwnerAsync("Trial Reg Co", "trial-reg@example.com");

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KadrDbContext>();
        var ambient = scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();
        ambient.Set(owner.TenantId);

        var sub = await db.Subscriptions.AsNoTracking().SingleAsync(s => s.TenantId == owner.TenantId);
        var plan = await db.Plans.AsNoTracking().SingleAsync(p => p.Id == sub.PlanId);
        Assert.Equal(PlanCodes.Starter, plan.Code);
        Assert.Equal(SubscriptionStatus.Trialing, sub.Status);
        Assert.NotNull(sub.TrialEndsAt);
        Assert.InRange(sub.TrialEndsAt.Value, DateTimeOffset.UtcNow.AddDays(13), DateTimeOffset.UtcNow.AddDays(15));

        var features = PlanFeaturesParser.Parse(plan.FeaturesJson);
        Assert.True(features[FeatureKeys.CoreHr]);
        Assert.True(features[FeatureKeys.Attendance]);
        Assert.False(features[FeatureKeys.Shifts]);
    }

    [Fact]
    public async Task Cache_invalidates_after_plan_change()
    {
        await factory.ResetDatabaseAsync();
        var owner = await RegisterOwnerAsync("Cache Sub Co", "cache-sub@example.com");
        var client = AuthedClient(owner.Token);

        var before = await client.GetFromJsonAsync<SubscriptionDto>("/api/v1/subscription", JsonOptions);
        Assert.Equal(PlanCodes.Starter, before!.Plan.Code);

        var change = await client.PostAsJsonAsync(
            "/api/v1/dev/subscription",
            new DevSubscriptionRequest(PlanCodes.Growth, null, null, null));
        Assert.Equal(HttpStatusCode.NoContent, change.StatusCode);

        var after = await client.GetFromJsonAsync<SubscriptionDto>("/api/v1/subscription", JsonOptions);
        Assert.Equal(PlanCodes.Growth, after!.Plan.Code);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KadrDbContext>();
        var audits = await db.AuditLogs.IgnoreQueryFilters()
            .CountAsync(a => a.TenantId == owner.TenantId && a.Action == "subscription.plan_changed");
        Assert.Equal(1, audits);
    }

    [Fact]
    public async Task Me_includes_subscription_summary()
    {
        await factory.ResetDatabaseAsync();
        var owner = await RegisterOwnerAsync("Me Sub Co", "me-sub@example.com");
        var me = await AuthedClient(owner.Token).GetFromJsonAsync<MeResponse>("/api/v1/me", JsonOptions);
        Assert.NotNull(me!.Subscription);
        Assert.Equal(SubscriptionStates.TrialRunning, me.Subscription.State);
        Assert.False(me.Subscription.ReadOnly);
        Assert.True(me.Subscription.Features[FeatureKeys.CoreHr]);
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
