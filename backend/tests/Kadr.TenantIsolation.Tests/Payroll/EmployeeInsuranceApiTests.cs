using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kadr.Identity.Application.Auth;
using Kadr.Identity.Domain.Roles;
using Kadr.Infrastructure.Persistence;
using Kadr.Platform.Application.Employees;
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
public sealed class EmployeeInsuranceApiTests(RegisterApiFactory factory)
{
    private static readonly JsonSerializerOptions JsonOptions = AuthTestHelper.JsonOptions;

    [Fact]
    public async Task Upsert_requires_fields_when_insured_and_returns_profile()
    {
        await factory.ResetDatabaseAsync();
        var helper = new AuthTestHelper(factory);
        var account = await helper.RegisterVerifiedAsync("Ins Co", "ins-create@example.com");
        var session = await helper.LoginSessionAsync(account.Email, AuthTestHelper.Password, account.Slug);
        var client = helper.Authed(session.AccessToken);
        var employeeId = await CreateEmployeeAsync(client);

        var empty = await client.GetAsync($"/api/v1/employees/{employeeId}/insurance");
        Assert.Equal(HttpStatusCode.OK, empty.StatusCode);
        Assert.Equal("null", (await empty.Content.ReadAsStringAsync()).Trim());

        var invalid = await client.PutAsJsonAsync(
            $"/api/v1/employees/{employeeId}/insurance",
            new UpsertEmployeeInsuranceRequest(true, null, null, null));
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Contains(
            ErrorCodes.InsuranceValidationFailed,
            await invalid.Content.ReadAsStringAsync(),
            StringComparison.Ordinal);

        var put = await client.PutAsJsonAsync(
            $"/api/v1/employees/{employeeId}/insurance",
            new UpsertEmployeeInsuranceRequest(
                true,
                "1234567890",
                new DateOnly(2020, 1, 15),
                8000m));
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        var created = await put.Content.ReadFromJsonAsync<EmployeeInsuranceProfileDto>(JsonOptions);
        Assert.NotNull(created);
        Assert.True(created.IsInsured);
        Assert.Equal("1234567890", created.InsuranceNumber);
        Assert.Equal(8000m, created.InsurableWageOverride);

        var update = await client.PutAsJsonAsync(
            $"/api/v1/employees/{employeeId}/insurance",
            new UpsertEmployeeInsuranceRequest(false, null, null, null));
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var updated = await update.Content.ReadFromJsonAsync<EmployeeInsuranceProfileDto>(JsonOptions);
        Assert.False(updated!.IsInsured);

        var get = await client.GetFromJsonAsync<EmployeeInsuranceProfileDto>(
            $"/api/v1/employees/{employeeId}/insurance",
            JsonOptions);
        Assert.NotNull(get);
        Assert.Equal(updated.Id, get.Id);
    }

    [Fact]
    public async Task Insurance_is_tenant_isolated_and_permission_gated()
    {
        await factory.ResetDatabaseAsync();
        var helper = new AuthTestHelper(factory);
        var a = await helper.RegisterVerifiedAsync("Ins Iso A", "ins-iso-a@example.com");
        var b = await helper.RegisterVerifiedAsync("Ins Iso B", "ins-iso-b@example.com");
        var clientA = helper.Authed(
            (await helper.LoginSessionAsync(a.Email, AuthTestHelper.Password, a.Slug)).AccessToken);
        var clientB = helper.Authed(
            (await helper.LoginSessionAsync(b.Email, AuthTestHelper.Password, b.Slug)).AccessToken);

        var employeeA = await CreateEmployeeAsync(clientA);
        var put = await clientA.PutAsJsonAsync(
            $"/api/v1/employees/{employeeA}/insurance",
            new UpsertEmployeeInsuranceRequest(true, "999", new DateOnly(2021, 5, 1), null));
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);

        var crossGet = await clientB.GetAsync($"/api/v1/employees/{employeeA}/insurance");
        Assert.Equal(HttpStatusCode.NotFound, crossGet.StatusCode);

        var crossPut = await clientB.PutAsJsonAsync(
            $"/api/v1/employees/{employeeA}/insurance",
            new UpsertEmployeeInsuranceRequest(false, null, null, null));
        Assert.Equal(HttpStatusCode.NotFound, crossPut.StatusCode);

        var account = await helper.RegisterVerifiedAsync("Ins Perm", "ins-perm@example.com");
        var owner = helper.Authed(
            (await helper.LoginSessionAsync(account.Email, AuthTestHelper.Password, account.Slug)).AccessToken);
        var employeeId = await CreateEmployeeAsync(owner);
        await DemoteToEmployeeAsync(account.TenantId, account.Email);
        var emp = helper.Authed(
            (await helper.LoginSessionAsync(account.Email, AuthTestHelper.Password, account.Slug)).AccessToken);

        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await emp.GetAsync($"/api/v1/employees/{employeeId}/insurance")).StatusCode);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await emp.PutAsJsonAsync(
                $"/api/v1/employees/{employeeId}/insurance",
                new UpsertEmployeeInsuranceRequest(false, null, null, null))).StatusCode);
    }

    private static async Task<Guid> CreateEmployeeAsync(HttpClient client)
    {
        var create = await client.PostAsJsonAsync(
            "/api/v1/employees",
            new
            {
                fullNameAr = "موظف تأمين",
                fullNameEn = "Ins Emp",
                hireDate = "2024-01-01",
                status = "Active",
            });
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var detail = await create.Content.ReadFromJsonAsync<EmployeeDetailDto>(JsonOptions);
        return detail!.Id;
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
