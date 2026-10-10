using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kadr.Api.Endpoints;
using Kadr.Infrastructure.Persistence;
using Kadr.Infrastructure.Persistence.Commands;
using Kadr.Infrastructure.Persistence.Users;
using Kadr.Platform.Application.Abstractions;
using Kadr.Platform.Application.Tenants;
using Kadr.SharedKernel.Constants;
using Kadr.SharedKernel.Tenancy;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Kadr.TenantIsolation.Tests.Register;

[Collection(RegisterCollection.Name)]
public sealed class RegisterTenantTests(RegisterApiFactory factory)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    [Fact]
    public async Task Happy_path_creates_tenant_trial_roles_owner_and_settings()
    {
        await factory.ResetDatabaseAsync();
        var client = factory.CreateClient();
        var response = await RegisterAsync(client, "Owner One", "owner1@example.com", "Acme Corp", Guid.NewGuid().ToString("N"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<RegisterTenantResponse>(JsonOptions);
        Assert.NotNull(body);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KadrDbContext>();
        var ambient = scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();
        ambient.Set(body.TenantId);

        var tenant = await db.Tenants.AsNoTracking().SingleAsync(t => t.Id == body.TenantId);
        Assert.Equal("Acme Corp", tenant.Name);
        Assert.Equal(body.Slug, tenant.Slug);

        var subscription = await db.Subscriptions.AsNoTracking().SingleAsync(s => s.TenantId == body.TenantId);
        var plan = await db.Plans.AsNoTracking().SingleAsync(p => p.Id == subscription.PlanId);
        Assert.Equal(PlanCodes.Starter, plan.Code);
        Assert.Equal(Kadr.Platform.Domain.Subscriptions.SubscriptionStatus.Trialing, subscription.Status);

        var roles = await db.Roles.AsNoTracking().Where(r => r.TenantId == body.TenantId).ToListAsync();
        Assert.Equal(5, roles.Count);
        Assert.All(roles, r => Assert.True(r.IsSystem));

        foreach (var role in roles)
        {
            var codes = await db.RolePermissions.AsNoTracking()
                .Where(p => p.RoleId == role.Id)
                .Select(p => p.PermissionCode)
                .ToListAsync();
            Assert.Equal(
                SystemRolePermissions.ForRole(role.Name).OrderBy(x => x),
                codes.OrderBy(x => x));
        }

        var owner = await db.Users.AsNoTracking().SingleAsync(u => u.TenantId == body.TenantId);
        Assert.Equal("owner1@example.com", owner.Email);
        Assert.False(owner.EmailConfirmed);

        var settings = await db.TenantSettings.AsNoTracking()
            .Where(s => s.TenantId == body.TenantId)
            .Select(s => s.Key)
            .ToListAsync();
        Assert.Contains(SettingKeys.CompanyCountry, settings);
        Assert.Contains(SettingKeys.WorkWeekDays, settings);
        Assert.Contains(SettingKeys.WorkWeekWeekend, settings);
        Assert.Contains(SettingKeys.DateFormat, settings);
        Assert.Contains(SettingKeys.CalendarHijriEnabled, settings);

        Assert.True(await db.EmailVerificationTokens.AsNoTracking().AnyAsync(t => t.UserId == owner.Id));
    }

    [Fact]
    public async Task Rollback_when_failure_injected_persists_nothing()
    {
        await factory.ResetDatabaseAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KadrDbContext>();
        var beforeTenants = await db.Tenants.IgnoreQueryFilters().CountAsync();

        var handler = ActivatorUtilities.CreateInstance<RegisterTenantCommandHandler>(
            scope.ServiceProvider,
            new ThrowingInjector());

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.HandleAsync(
            new RegisterTenantCommand(
                "Owner",
                "rollback@example.com",
                "Str0ngPass!!",
                "Rollback Co",
                "office",
                null,
                "ar",
                "EG"),
            CancellationToken.None));

        Assert.Equal(beforeTenants, await db.Tenants.IgnoreQueryFilters().CountAsync());
        Assert.Equal(0, await db.Users.IgnoreQueryFilters().CountAsync());
        Assert.Equal(0, await db.Roles.IgnoreQueryFilters().CountAsync());
        Assert.Equal(0, await db.Subscriptions.IgnoreQueryFilters().CountAsync());
    }

    [Fact]
    public async Task Same_email_allowed_across_tenants_but_rejected_within_tenant()
    {
        await factory.ResetDatabaseAsync();
        var client = factory.CreateClient();
        const string email = "shared@example.com";

        var first = await RegisterAsync(client, "A", email, "Tenant A", Guid.NewGuid().ToString("N"));
        var second = await RegisterAsync(client, "B", email, "Tenant B", Guid.NewGuid().ToString("N"));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);

        var body = await first.Content.ReadFromJsonAsync<RegisterTenantResponse>(JsonOptions);
        Assert.NotNull(body);

        using var scope = factory.Services.CreateScope();
        var ambient = scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();
        ambient.Set(body.TenantId);
        var checker = scope.ServiceProvider.GetRequiredService<IEmailUniquenessChecker>();
        var result = await checker.EnsureAvailableAsync(body.TenantId, EmailNormalizer.Normalize(email));
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.AuthEmailTaken, result.ErrorCode);
    }

    [Fact]
    public async Task Idempotency_key_twice_creates_exactly_one_tenant()
    {
        await factory.ResetDatabaseAsync();
        var client = factory.CreateClient();
        var key = Guid.NewGuid().ToString("N");

        var first = await RegisterAsync(client, "Idem Owner", "idem@example.com", "Idem Co", key);
        var second = await RegisterAsync(client, "Idem Owner", "idem@example.com", "Idem Co", key);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);

        var a = await first.Content.ReadFromJsonAsync<RegisterTenantResponse>(JsonOptions);
        var b = await second.Content.ReadFromJsonAsync<RegisterTenantResponse>(JsonOptions);
        Assert.Equal(a!.TenantId, b!.TenantId);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KadrDbContext>();
        Assert.Equal(1, await db.Tenants.IgnoreQueryFilters().CountAsync());
    }

    [Theory]
    [InlineData("ar", "البريد الإلكتروني مستخدم بالفعل في هذه المنشأة.")]
    [InlineData("en", "This email is already used in this tenant.")]
    public void Localizer_returns_auth_email_taken_in_language(string lang, string expected)
    {
        var localizer = new Kadr.Api.Localization.ErrorLocalizer();
        Assert.Equal(expected, localizer.GetDetail(ErrorCodes.AuthEmailTaken, lang));
    }

    [Fact]
    public void Register_endpoint_requires_rate_limiting_policy()
    {
        _ = factory.CreateClient();
        var source = factory.Services.GetRequiredService<EndpointDataSource>();
        var endpoint = source.Endpoints
            .OfType<RouteEndpoint>()
            .Single(e => e.RoutePattern.RawText == "/api/v1/auth/register");

        var metadata = endpoint.Metadata.GetMetadata<EnableRateLimitingAttribute>();
        Assert.NotNull(metadata);
        Assert.Equal("auth-register", metadata.PolicyName);
    }

    internal static async Task<HttpResponseMessage> RegisterAsync(
        HttpClient client,
        string fullName,
        string email,
        string companyName,
        string idempotencyKey)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/register")
        {
            Content = JsonContent.Create(new RegisterTenantRequest(
                fullName,
                email,
                "Str0ngPass!!",
                companyName,
                "office",
                "Africa/Cairo",
                "ar",
                "EG")),
        };
        request.Headers.TryAddWithoutValidation(HttpHeaderNames.IdempotencyKey, idempotencyKey);
        return await client.SendAsync(request);
    }

    private sealed class ThrowingInjector : IRegisterTenantFailureInjector
    {
        public void AfterTenantInserted() => throw new InvalidOperationException("forced failure");
    }
}

[CollectionDefinition(Name)]
public sealed class RegisterCollection : ICollectionFixture<RegisterApiFactory>
{
    public const string Name = "register-api";
}
