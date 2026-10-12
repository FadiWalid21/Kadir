using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Kadr.Api.Endpoints;
using Kadr.Identity.Application.Auth;
using Kadr.Identity.Domain.Roles;
using Kadr.Identity.Domain.Users;
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
public sealed class PayrollRulesApiTests(RegisterApiFactory factory)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private const string Password = "Str0ngPass!!";
    private const string BasePath = "/api/v1/payroll/rules";

    [Fact]
    public async Task List_upsert_confirm_and_forbid_without_permission()
    {
        await factory.ResetDatabaseAsync();
        var session = await RegisterAndLoginAsync("Rules Api Co", "rules-api@example.com");

        var list = await session.Client.GetFromJsonAsync<PayrollRulesListDto>(BasePath, JsonOptions);
        Assert.NotNull(list);
        Assert.Equal(PayrollRuleKeys.All.Count, list.Items.Count);
        Assert.False(list.RulesConfirmedForLock);
        Assert.Contains(list.Items, i => i.RuleKey == PayrollRuleKeys.InsuranceWageMin && !i.IsVerified);

        var upsert = await session.Client.PutAsJsonAsync(
            $"{BasePath}/{PayrollRuleKeys.InsuranceEmployeeRate}",
            new UpsertPayrollRuleRequest(
                """{"rate":12}""",
                new DateOnly(2026, 1, 1),
                "Company adjustment"));
        Assert.Equal(HttpStatusCode.OK, upsert.StatusCode);
        var updated = await upsert.Content.ReadFromJsonAsync<PayrollRuleListItemDto>(JsonOptions);
        Assert.NotNull(updated);
        Assert.True(updated.IsCustomized);
        Assert.True(updated.DiffersFromOfficialDefault);

        var confirm = await session.Client.PostAsJsonAsync(
            $"{BasePath}/confirm",
            new ConfirmPayrollRulesRequest("Confirmed [VERIFY] placeholders for demo"));
        Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);

        var after = await session.Client.GetFromJsonAsync<PayrollRulesListDto>(BasePath, JsonOptions);
        Assert.NotNull(after);
        Assert.True(after.RulesConfirmedForLock);

        var setup = factory.CreateCookieClient();
        var reg = await RegisterVerifiedAsync(setup, "NoPerm Rules", "rules-noperm@example.com");
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
        Assert.Equal(HttpStatusCode.Forbidden, (await emp.GetAsync(BasePath)).StatusCode);
    }

    [Fact]
    public async Task Invalid_rate_rejected()
    {
        await factory.ResetDatabaseAsync();
        var session = await RegisterAndLoginAsync("Rules Bad Co", "rules-bad@example.com");

        var bad = await session.Client.PutAsJsonAsync(
            $"{BasePath}/{PayrollRuleKeys.InsuranceEmployeeRate}",
            new UpsertPayrollRuleRequest(
                """{"rate":150}""",
                new DateOnly(2026, 1, 1),
                "bad"));
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        Assert.Contains(ErrorCodes.PayrollRulesValidationFailed, await bad.Content.ReadAsStringAsync());
    }

    private async Task<(HttpClient Client, Guid TenantId)> RegisterAndLoginAsync(string company, string email)
    {
        var client = factory.CreateCookieClient();
        var reg = await RegisterVerifiedAsync(client, company, email);
        var token = await LoginAccessTokenAsync(client, email, reg.Slug);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return (client, reg.TenantId);
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
        await client.PostAsJsonAsync("/api/v1/auth/verify-email", new VerifyEmailRequest(token));
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
