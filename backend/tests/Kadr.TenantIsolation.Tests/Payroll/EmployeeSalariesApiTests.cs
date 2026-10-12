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
public sealed class EmployeeSalariesApiTests(RegisterApiFactory factory)
{
    private static readonly JsonSerializerOptions JsonOptions = AuthTestHelper.JsonOptions;

    [Fact]
    public async Task Create_closes_previous_and_rejects_overlap_and_basic_component()
    {
        await factory.ResetDatabaseAsync();
        var helper = new AuthTestHelper(factory);
        var account = await helper.RegisterVerifiedAsync("Sal Co", "sal-create@example.com");
        var session = await helper.LoginSessionAsync(account.Email, AuthTestHelper.Password, account.Slug);
        var client = helper.Authed(session.AccessToken);

        var employeeId = await CreateEmployeeAsync(client);
        var housingId = await CreateHousingComponentAsync(client);

        var currentEmptyResponse = await client.GetAsync(
            $"/api/v1/employees/{employeeId}/salaries/current");
        Assert.Equal(HttpStatusCode.OK, currentEmptyResponse.StatusCode);
        var currentEmptyBody = await currentEmptyResponse.Content.ReadAsStringAsync();
        Assert.Equal("null", currentEmptyBody.Trim());

        var first = await client.PostAsJsonAsync(
            $"/api/v1/employees/{employeeId}/salaries",
            new CreateEmployeeSalaryRequest(
                new DateOnly(2024, 1, 1),
                "EGP",
                5000m,
                "Initial hire",
                [new CreateEmployeeSalaryComponentInput(housingId, 500m)]));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var firstDto = await first.Content.ReadFromJsonAsync<EmployeeSalaryDto>(JsonOptions);
        Assert.NotNull(firstDto);
        Assert.Null(firstDto.EffectiveTo);
        Assert.Equal(5000m, firstDto.BasicAmount);
        Assert.Single(firstDto.Components);
        Assert.True(firstDto.Components[0].IsTaxable);
        Assert.False(firstDto.Components[0].IsInsurable);

        var historyAfterFirst = await client.GetFromJsonAsync<PagedEmployeeSalariesDto>(
            $"/api/v1/employees/{employeeId}/salaries?page=1&pageSize=10",
            JsonOptions);
        Assert.Equal(5500m, historyAfterFirst!.Items[0].GrossAmount);

        var payComponents = await client.GetFromJsonAsync<PagedPayComponentsDto>(
            "/api/v1/payroll/pay-components?page=1&pageSize=50",
            JsonOptions);
        var basicId = payComponents!.Items.Single(c => c.Key == "basic").Id;
        var withBasic = await client.PostAsJsonAsync(
            $"/api/v1/employees/{employeeId}/salaries",
            new CreateEmployeeSalaryRequest(
                new DateOnly(2024, 6, 1),
                "EGP",
                5500m,
                "Bad basic line",
                [new CreateEmployeeSalaryComponentInput(basicId, 5500m)]));
        Assert.Equal(HttpStatusCode.BadRequest, withBasic.StatusCode);
        Assert.Contains(
            ErrorCodes.SalaryInvalidComponent,
            await withBasic.Content.ReadAsStringAsync(),
            StringComparison.Ordinal);

        var overlap = await client.PostAsJsonAsync(
            $"/api/v1/employees/{employeeId}/salaries",
            new CreateEmployeeSalaryRequest(
                new DateOnly(2023, 12, 1),
                "EGP",
                4000m,
                "Too early",
                null));
        Assert.Equal(HttpStatusCode.BadRequest, overlap.StatusCode);
        Assert.Contains(
            ErrorCodes.SalaryEffectiveOverlap,
            await overlap.Content.ReadAsStringAsync(),
            StringComparison.Ordinal);

        var second = await client.PostAsJsonAsync(
            $"/api/v1/employees/{employeeId}/salaries",
            new CreateEmployeeSalaryRequest(
                new DateOnly(2024, 6, 1),
                "EGP",
                6000m,
                "Raise",
                [new CreateEmployeeSalaryComponentInput(housingId, 700m)]));
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var secondDto = await second.Content.ReadFromJsonAsync<EmployeeSalaryDto>(JsonOptions);
        Assert.NotNull(secondDto);
        Assert.Equal(6000m, secondDto.BasicAmount);

        var history = await client.GetFromJsonAsync<PagedEmployeeSalariesDto>(
            $"/api/v1/employees/{employeeId}/salaries?page=1&pageSize=50",
            JsonOptions);
        Assert.Equal(2, history!.TotalCount);
        Assert.Equal(secondDto.Id, history.Items[0].Id);
        Assert.Equal(firstDto.Id, history.Items[1].Id);
        Assert.Equal(new DateOnly(2024, 5, 31), history.Items[1].EffectiveTo);

        var current = await client.GetFromJsonAsync<EmployeeSalaryDto>(
            $"/api/v1/employees/{employeeId}/salaries/current",
            JsonOptions);
        Assert.NotNull(current);
        Assert.Equal(secondDto.Id, current.Id);
    }

    [Fact]
    public async Task Salaries_are_tenant_isolated_and_permission_gated()
    {
        await factory.ResetDatabaseAsync();
        var helper = new AuthTestHelper(factory);
        var a = await helper.RegisterVerifiedAsync("Sal Iso A", "sal-iso-a@example.com");
        var b = await helper.RegisterVerifiedAsync("Sal Iso B", "sal-iso-b@example.com");
        var sessionA = await helper.LoginSessionAsync(a.Email, AuthTestHelper.Password, a.Slug);
        var sessionB = await helper.LoginSessionAsync(b.Email, AuthTestHelper.Password, b.Slug);
        var clientA = helper.Authed(sessionA.AccessToken);
        var clientB = helper.Authed(sessionB.AccessToken);

        var employeeA = await CreateEmployeeAsync(clientA);
        var create = await clientA.PostAsJsonAsync(
            $"/api/v1/employees/{employeeA}/salaries",
            new CreateEmployeeSalaryRequest(
                new DateOnly(2024, 3, 1),
                "EGP",
                3000m,
                "A salary",
                null));
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var salary = await create.Content.ReadFromJsonAsync<EmployeeSalaryDto>(JsonOptions);

        var crossList = await clientB.GetAsync($"/api/v1/employees/{employeeA}/salaries");
        Assert.Equal(HttpStatusCode.NotFound, crossList.StatusCode);

        var crossGet = await clientB.GetAsync($"/api/v1/employees/{employeeA}/salaries/{salary!.Id}");
        Assert.Equal(HttpStatusCode.NotFound, crossGet.StatusCode);

        var crossCreate = await clientB.PostAsJsonAsync(
            $"/api/v1/employees/{employeeA}/salaries",
            new CreateEmployeeSalaryRequest(
                new DateOnly(2024, 4, 1),
                "EGP",
                1m,
                "Hack",
                null));
        Assert.Equal(HttpStatusCode.NotFound, crossCreate.StatusCode);
    }

    [Fact]
    public async Task Employee_role_cannot_read_or_write_salaries()
    {
        await factory.ResetDatabaseAsync();
        var helper = new AuthTestHelper(factory);
        var account = await helper.RegisterVerifiedAsync("Sal Perm", "sal-perm@example.com");
        var ownerSession = await helper.LoginSessionAsync(account.Email, AuthTestHelper.Password, account.Slug);
        var owner = helper.Authed(ownerSession.AccessToken);
        var employeeId = await CreateEmployeeAsync(owner);
        await owner.PostAsJsonAsync(
            $"/api/v1/employees/{employeeId}/salaries",
            new CreateEmployeeSalaryRequest(new DateOnly(2024, 1, 1), "EGP", 1000m, "Seed", null));

        await DemoteToEmployeeAsync(account.TenantId, account.Email);
        var empSession = await helper.LoginSessionAsync(account.Email, AuthTestHelper.Password, account.Slug);
        var emp = helper.Authed(empSession.AccessToken);

        var forbiddenRead = await emp.GetAsync($"/api/v1/employees/{employeeId}/salaries");
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenRead.StatusCode);

        var forbiddenWrite = await emp.PostAsJsonAsync(
            $"/api/v1/employees/{employeeId}/salaries",
            new CreateEmployeeSalaryRequest(new DateOnly(2024, 7, 1), "EGP", 2000m, "No", null));
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenWrite.StatusCode);
    }

    private static async Task<Guid> CreateEmployeeAsync(HttpClient client)
    {
        var create = await client.PostAsJsonAsync(
            "/api/v1/employees",
            new
            {
                fullNameAr = "موظف راتب",
                fullNameEn = "Salary Emp",
                hireDate = "2024-01-01",
                status = "Active",
            });
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var detail = await create.Content.ReadFromJsonAsync<EmployeeDetailDto>(JsonOptions);
        return detail!.Id;
    }

    private static async Task<Guid> CreateHousingComponentAsync(HttpClient client)
    {
        // Ensure basic is seeded via list.
        await client.GetAsync("/api/v1/payroll/pay-components?page=1&pageSize=10");
        var create = await client.PostAsJsonAsync(
            "/api/v1/payroll/pay-components",
            new CreatePayComponentRequest(
                "housing",
                "بدل سكن",
                "Housing",
                "Earning",
                "Fixed",
                IsTaxable: true,
                IsInsurable: false));
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var dto = await create.Content.ReadFromJsonAsync<PayComponentDto>(JsonOptions);
        return dto!.Id;
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
