using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
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
public sealed class SalaryImportApiTests(RegisterApiFactory factory)
{
    private static readonly JsonSerializerOptions JsonOptions = AuthTestHelper.JsonOptions;
    private const string Base = "/api/v1/payroll/salaries/import";

    [Fact]
    public async Task Preview_and_commit_salary_csv()
    {
        await factory.ResetDatabaseAsync();
        var helper = new AuthTestHelper(factory);
        var account = await helper.RegisterVerifiedAsync("Imp Sal Co", "imp-sal@example.com");
        var client = helper.Authed(
            (await helper.LoginSessionAsync(account.Email, AuthTestHelper.Password, account.Slug)).AccessToken);

        var employeeId = await CreateEmployeeAsync(client);
        var detail = await client.GetFromJsonAsync<EmployeeDetailDto>($"/api/v1/employees/{employeeId}", JsonOptions);
        var number = detail!.EmployeeNumber;

        await client.GetAsync("/api/v1/payroll/pay-components?page=1&pageSize=10");
        var housing = await client.PostAsJsonAsync(
            "/api/v1/payroll/pay-components",
            new CreatePayComponentRequest(
                "housing",
                "بدل سكن",
                "Housing",
                "Earning",
                "Fixed",
                true,
                false));
        Assert.Equal(HttpStatusCode.OK, housing.StatusCode);

        var template = await client.GetAsync($"{Base}/template");
        Assert.Equal(HttpStatusCode.OK, template.StatusCode);

        var csv =
            "employeeNumber,effectiveFrom,basicAmount,changeReason,currency,housing\n" +
            $"{number},2024-01-01,5000,Initial,EGP,500\n" +
            "MISSING,2024-02-01,1,Bad,,\n";

        using var previewContent = new MultipartFormDataContent();
        previewContent.Add(new ByteArrayContent(Encoding.UTF8.GetBytes(csv)), "file", "salaries.csv");
        var preview = await client.PostAsync($"{Base}/preview", previewContent);
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        var previewDto = await preview.Content.ReadFromJsonAsync<SalaryImportPreviewResultDto>(JsonOptions);
        Assert.NotNull(previewDto);
        Assert.Equal(1, previewDto.ValidCount);
        Assert.True(previewDto.ErrorCount >= 1);
        Assert.Single(previewDto.ValidRows);

        var overlapCsv =
            "employeeNumber,effectiveFrom,basicAmount,changeReason\n" +
            $"{number},2023-01-01,1000,Too early\n";
        // First commit the valid row so open period exists, then overlap preview.
        var commit = await client.PostAsJsonAsync($"{Base}/commit", previewDto.ValidRows);
        Assert.Equal(HttpStatusCode.OK, commit.StatusCode);
        var commitDto = await commit.Content.ReadFromJsonAsync<SalaryImportCommitResultDto>(JsonOptions);
        Assert.Equal(1, commitDto!.ImportedCount);

        using var overlapContent = new MultipartFormDataContent();
        overlapContent.Add(new ByteArrayContent(Encoding.UTF8.GetBytes(overlapCsv)), "file", "overlap.csv");
        var overlapPreview = await client.PostAsync($"{Base}/preview", overlapContent);
        Assert.Equal(HttpStatusCode.OK, overlapPreview.StatusCode);
        var overlapDto = await overlapPreview.Content.ReadFromJsonAsync<SalaryImportPreviewResultDto>(JsonOptions);
        Assert.Equal(0, overlapDto!.ValidCount);
        Assert.Contains(overlapDto.Errors, e => e.Code == "overlap");

        var history = await client.GetFromJsonAsync<PagedEmployeeSalariesDto>(
            $"/api/v1/employees/{employeeId}/salaries?page=1&pageSize=10",
            JsonOptions);
        Assert.Equal(1, history!.TotalCount);
        Assert.Equal(5000m, history.Items[0].BasicAmount);
    }

    [Fact]
    public async Task Preview_rejects_bad_component_key()
    {
        await factory.ResetDatabaseAsync();
        var helper = new AuthTestHelper(factory);
        var account = await helper.RegisterVerifiedAsync("Imp Comp", "imp-comp@example.com");
        var client = helper.Authed(
            (await helper.LoginSessionAsync(account.Email, AuthTestHelper.Password, account.Slug)).AccessToken);

        var employeeId = await CreateEmployeeAsync(client);
        var detail = await client.GetFromJsonAsync<EmployeeDetailDto>($"/api/v1/employees/{employeeId}", JsonOptions);
        var number = detail!.EmployeeNumber;

        var csv =
            "employeeNumber,effectiveFrom,basicAmount,changeReason,not_a_real_key\n" +
            $"{number},2024-01-01,5000,Initial,100\n";
        using var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent(Encoding.UTF8.GetBytes(csv)), "file", "bad.csv");
        var preview = await client.PostAsync($"{Base}/preview", content);
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        var dto = await preview.Content.ReadFromJsonAsync<SalaryImportPreviewResultDto>(JsonOptions);
        Assert.Equal(0, dto!.ValidCount);
        Assert.Contains(dto.Errors, e => e.Code == "invalid_component");
    }

    [Fact]
    public async Task Import_cannot_use_other_tenant_employee_number()
    {
        await factory.ResetDatabaseAsync();
        var helper = new AuthTestHelper(factory);
        var a = await helper.RegisterVerifiedAsync("Imp Iso A", "imp-iso-a@example.com");
        var b = await helper.RegisterVerifiedAsync("Imp Iso B", "imp-iso-b@example.com");
        var clientA = helper.Authed(
            (await helper.LoginSessionAsync(a.Email, AuthTestHelper.Password, a.Slug)).AccessToken);
        var clientB = helper.Authed(
            (await helper.LoginSessionAsync(b.Email, AuthTestHelper.Password, b.Slug)).AccessToken);

        var employeeId = await CreateEmployeeAsync(clientA);
        var detail = await clientA.GetFromJsonAsync<EmployeeDetailDto>($"/api/v1/employees/{employeeId}", JsonOptions);
        var number = detail!.EmployeeNumber;

        var csv =
            "employeeNumber,effectiveFrom,basicAmount,changeReason\n" +
            $"{number},2024-01-01,5000,Cross tenant\n";
        using var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent(Encoding.UTF8.GetBytes(csv)), "file", "iso.csv");
        var preview = await clientB.PostAsync($"{Base}/preview", content);
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        var dto = await preview.Content.ReadFromJsonAsync<SalaryImportPreviewResultDto>(JsonOptions);
        Assert.Equal(0, dto!.ValidCount);
        Assert.Contains(dto.Errors, e => e.Code == "not_found");
    }

    [Fact]
    public async Task Import_forbidden_without_salary_manage()
    {
        await factory.ResetDatabaseAsync();
        var helper = new AuthTestHelper(factory);
        var account = await helper.RegisterVerifiedAsync("Imp Perm", "imp-perm@example.com");
        await DemoteToEmployeeAsync(account.TenantId, account.Email);
        var emp = helper.Authed(
            (await helper.LoginSessionAsync(account.Email, AuthTestHelper.Password, account.Slug)).AccessToken);

        Assert.Equal(HttpStatusCode.Forbidden, (await emp.GetAsync($"{Base}/template")).StatusCode);
    }

    private static async Task<Guid> CreateEmployeeAsync(HttpClient client)
    {
        var create = await client.PostAsJsonAsync(
            "/api/v1/employees",
            new
            {
                fullNameAr = "موظف استيراد",
                fullNameEn = "Import Emp",
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
