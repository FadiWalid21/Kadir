using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kadr.Api.Endpoints;
using Kadr.Identity.Application.Auth;
using Kadr.Infrastructure.Persistence;
using Kadr.SharedKernel.Constants;
using Kadr.SharedKernel.Tenancy;
using Kadr.TenantIsolation.Tests.Register;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Kadr.TenantIsolation.Tests.Payroll;

[Collection(RegisterCollection.Name)]
public sealed class TenantRuleValuesIsolationTests(RegisterApiFactory factory)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private const string Password = "Str0ngPass!!";

    [Fact]
    public async Task Register_copies_platform_rules_and_query_filter_hides_other_tenant()
    {
        await factory.ResetDatabaseAsync();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KadrDbContext>();
            Assert.True(
                await db.StatutoryRuleSets.AnyAsync(x => x.RuleKey == PayrollRuleKeys.InsuranceWageMin));
        }

        var a = await RegisterVerifiedAsync("Rules A Co", "rules-a@example.com");
        var b = await RegisterVerifiedAsync("Rules B Co", "rules-b@example.com");

        using var scopeA = factory.Services.CreateScope();
        var accessor = scopeA.ServiceProvider.GetRequiredService<ITenantContextAccessor>();
        var dbA = scopeA.ServiceProvider.GetRequiredService<KadrDbContext>();
        accessor.Set(a.TenantId);

        // Platform copy + OT settings migration may add a second effective-dated row per OT key.
        var keysA = await dbA.TenantRuleValues.Select(x => x.RuleKey).Distinct().CountAsync();
        Assert.Equal(PayrollRuleKeys.All.Count, keysA);

        var foreign = await dbA.TenantRuleValues.IgnoreQueryFilters()
            .Where(x => x.TenantId == b.TenantId)
            .Select(x => x.Id)
            .FirstAsync();

        Assert.Null(await dbA.TenantRuleValues.FirstOrDefaultAsync(x => x.Id == foreign));
    }

    private async Task<(string Slug, Guid TenantId)> RegisterVerifiedAsync(string companyName, string email)
    {
        var client = factory.CreateCookieClient();
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
        return (reg.Slug, reg.TenantId);
    }
}
