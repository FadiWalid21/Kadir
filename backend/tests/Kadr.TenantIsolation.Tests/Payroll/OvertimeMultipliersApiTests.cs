using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kadr.Identity.Application.Auth;
using Kadr.Identity.Domain.Roles;
using Kadr.Infrastructure.Persistence;
using Kadr.Platform.Application.Payroll;
using Kadr.SharedKernel.Constants;
using Kadr.SharedKernel.Tenancy;
using Kadr.TenantIsolation.Tests.Auth;
using Kadr.TenantIsolation.Tests.Register;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Kadr.TenantIsolation.Tests.Payroll;

[Collection(RegisterCollection.Name)]
public sealed class OvertimeMultipliersApiTests(RegisterApiFactory factory)
{
    private static readonly JsonSerializerOptions JsonOptions = AuthTestHelper.JsonOptions;

    [Fact]
    public async Task Get_returns_seeded_multipliers_and_is_permission_gated()
    {
        await factory.ResetDatabaseAsync();
        var helper = new AuthTestHelper(factory);
        var account = await helper.RegisterVerifiedAsync("Ot Co", "ot-mult@example.com");
        var client = helper.Authed(
            (await helper.LoginSessionAsync(account.Email, AuthTestHelper.Password, account.Slug)).AccessToken);

        var ok = await client.GetFromJsonAsync<OvertimeMultipliersDto>(
            "/api/v1/payroll/overtime-multipliers",
            JsonOptions);
        Assert.NotNull(ok);
        Assert.Equal(1.35m, ok.Day);
        Assert.Equal(1.70m, ok.Night);
        Assert.Equal(2.00m, ok.Holiday);

        await DemoteToEmployeeAsync(account.TenantId, account.Email);
        var emp = helper.Authed(
            (await helper.LoginSessionAsync(account.Email, AuthTestHelper.Password, account.Slug)).AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, (await emp.GetAsync("/api/v1/payroll/overtime-multipliers")).StatusCode);
    }

    private async Task DemoteToEmployeeAsync(Guid tenantId, string email)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KadrDbContext>();
        var ambient = scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();
        ambient.Set(tenantId);
        var user = await db.Users.SingleAsync(u => u.Email == email);
        var employeeRole = await db.Roles.SingleAsync(r =>
            r.TenantId == tenantId && r.Name == SystemRoleNames.Employee);
        var ownerLink = await db.UserRoles.SingleAsync(ur => ur.UserId == user.Id);
        db.UserRoles.Remove(ownerLink);
        db.UserRoles.Add(UserRole.Create(tenantId, user.Id, employeeRole.Id));
        await db.SaveChangesAsync();
        scope.ServiceProvider.GetRequiredService<IPermissionCacheInvalidator>().Invalidate(tenantId, user.Id);
    }
}
