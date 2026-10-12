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
public sealed class PayrollRunsApiTests(RegisterApiFactory factory)
{
    private static readonly JsonSerializerOptions JsonOptions = AuthTestHelper.JsonOptions;
    private const string BasePath = "/api/v1/payroll/runs";

    [Fact]
    public async Task Create_list_get_are_tenant_isolated()
    {
        await factory.ResetDatabaseAsync();
        var helper = new AuthTestHelper(factory);
        var a = await helper.RegisterVerifiedAsync("Run Iso A", "run-iso-a@example.com");
        var b = await helper.RegisterVerifiedAsync("Run Iso B", "run-iso-b@example.com");
        var clientA = helper.Authed((await helper.LoginSessionAsync(a.Email, AuthTestHelper.Password, a.Slug)).AccessToken);
        var clientB = helper.Authed((await helper.LoginSessionAsync(b.Email, AuthTestHelper.Password, b.Slug)).AccessToken);

        var create = await clientA.PostAsJsonAsync(
            BasePath,
            new CreatePayrollRunRequest(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31)));
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var run = await create.Content.ReadFromJsonAsync<PayrollRunDetailDto>(JsonOptions);
        Assert.NotNull(run);
        Assert.Equal("Draft", run.Status);

        var listA = await clientA.GetFromJsonAsync<PagedPayrollRunsDto>($"{BasePath}?page=1&pageSize=10", JsonOptions);
        Assert.Contains(listA!.Items, x => x.Id == run.Id);

        var listB = await clientB.GetFromJsonAsync<PagedPayrollRunsDto>($"{BasePath}?page=1&pageSize=10", JsonOptions);
        Assert.DoesNotContain(listB!.Items, x => x.Id == run.Id);

        Assert.Equal(HttpStatusCode.NotFound, (await clientB.GetAsync($"{BasePath}/{run.Id}")).StatusCode);
    }

    [Fact]
    public async Task Calculate_produces_entry_for_employee_with_salary()
    {
        await factory.ResetDatabaseAsync();
        var helper = new AuthTestHelper(factory);
        var account = await helper.RegisterVerifiedAsync("Run Calc Co", "run-calc@example.com");
        var client = helper.Authed(
            (await helper.LoginSessionAsync(account.Email, AuthTestHelper.Password, account.Slug)).AccessToken);

        var employeeId = await CreateEmployeeAsync(client, "موظف مسير", "Payroll Emp");
        await client.PostAsJsonAsync(
            $"/api/v1/employees/{employeeId}/salaries",
            new CreateEmployeeSalaryRequest(new DateOnly(2025, 1, 1), "EGP", 8000m, "Hire", null));

        var create = await client.PostAsJsonAsync(
            BasePath,
            new CreatePayrollRunRequest(new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31)));
        var run = await create.Content.ReadFromJsonAsync<PayrollRunDetailDto>(JsonOptions);
        Assert.NotNull(run);

        var calc = await client.PostAsync($"{BasePath}/{run.Id}/calculate", null);
        Assert.Equal(HttpStatusCode.OK, calc.StatusCode);
        var after = await calc.Content.ReadFromJsonAsync<PayrollRunDetailDto>(JsonOptions);
        Assert.Equal("Draft", after!.Status);
        Assert.Equal(1, after.Headcount);
        Assert.True(after.TotalGross > 0);

        var entries = await client.GetFromJsonAsync<PagedPayrollEntriesDto>(
            $"{BasePath}/{run.Id}/entries?page=1&pageSize=50",
            JsonOptions);
        Assert.NotNull(entries);
        var entry = Assert.Single(entries.Items);
        Assert.Equal(employeeId, entry.EmployeeId);
        Assert.True(entry.Gross > 0);

        var detail = await client.GetFromJsonAsync<PayrollEntryDetailDto>(
            $"{BasePath}/entries/{entry.Id}",
            JsonOptions);
        Assert.NotNull(detail);
        Assert.NotEmpty(detail.Lines);
    }

    [Fact]
    public async Task Lock_blocked_without_rules_confirm_then_works_after_confirm_and_reopen()
    {
        await factory.ResetDatabaseAsync();
        var helper = new AuthTestHelper(factory);
        var account = await helper.RegisterVerifiedAsync("Run Lock Co", "run-lock@example.com");
        var client = helper.Authed(
            (await helper.LoginSessionAsync(account.Email, AuthTestHelper.Password, account.Slug)).AccessToken);

        // Allow creator to approve for this flow (must post-date seeded 2024-01-01 defaults).
        var upsert = await client.PutAsJsonAsync(
            $"/api/v1/payroll/rules/{PayrollRuleKeys.PayrollSecondApprovalRequired}",
            new UpsertPayrollRuleRequest(
                """{"value":false}""",
                new DateOnly(2025, 6, 1),
                "Test single approver"));
        Assert.Equal(HttpStatusCode.OK, upsert.StatusCode);

        var employeeId = await CreateEmployeeAsync(client, "موظف قفل", "Lock Emp");
        await client.PostAsJsonAsync(
            $"/api/v1/employees/{employeeId}/salaries",
            new CreateEmployeeSalaryRequest(new DateOnly(2025, 1, 1), "EGP", 5000m, "Hire", null));

        var create = await client.PostAsJsonAsync(
            BasePath,
            new CreatePayrollRunRequest(new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 30)));
        var run = await create.Content.ReadFromJsonAsync<PayrollRunDetailDto>(JsonOptions);
        Assert.NotNull(run);

        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"{BasePath}/{run.Id}/calculate", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"{BasePath}/{run.Id}/submit", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"{BasePath}/{run.Id}/approve", null)).StatusCode);

        var lockBlocked = await client.PostAsync($"{BasePath}/{run.Id}/lock", null);
        Assert.Equal(HttpStatusCode.BadRequest, lockBlocked.StatusCode);
        Assert.Contains(
            ErrorCodes.PayrollRulesNotConfirmed,
            await lockBlocked.Content.ReadAsStringAsync(),
            StringComparison.Ordinal);

        var confirm = await client.PostAsJsonAsync(
            "/api/v1/payroll/rules/confirm",
            new ConfirmPayrollRulesRequest("Confirmed for lock test [VERIFY]"));
        Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);

        var lockOk = await client.PostAsync($"{BasePath}/{run.Id}/lock", null);
        Assert.Equal(HttpStatusCode.OK, lockOk.StatusCode);
        var locked = await lockOk.Content.ReadFromJsonAsync<PayrollRunDetailDto>(JsonOptions);
        Assert.Equal("Locked", locked!.Status);

        var reopen = await client.PostAsJsonAsync(
            $"{BasePath}/{run.Id}/reopen",
            new ReopenPayrollRunRequest("Correction needed"));
        Assert.Equal(HttpStatusCode.OK, reopen.StatusCode);
        var draft = await reopen.Content.ReadFromJsonAsync<PayrollRunDetailDto>(JsonOptions);
        Assert.NotNull(draft);
        Assert.Equal("Draft", draft.Status);
        Assert.Equal(run.Id, draft.ReopenedFromRunId);

        var old = await client.GetFromJsonAsync<PayrollRunDetailDto>($"{BasePath}/{run.Id}", JsonOptions);
        Assert.Equal("Superseded", old!.Status);
    }

    [Fact]
    public async Task Concurrent_submit_returns_conflict()
    {
        await factory.ResetDatabaseAsync();
        var helper = new AuthTestHelper(factory);
        var account = await helper.RegisterVerifiedAsync("Run Conc Co", "run-conc@example.com");
        var client = helper.Authed(
            (await helper.LoginSessionAsync(account.Email, AuthTestHelper.Password, account.Slug)).AccessToken);

        var create = await client.PostAsJsonAsync(
            BasePath,
            new CreatePayrollRunRequest(new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 31)));
        var run = await create.Content.ReadFromJsonAsync<PayrollRunDetailDto>(JsonOptions);
        Assert.NotNull(run);

        using var scope1 = factory.Services.CreateScope();
        using var scope2 = factory.Services.CreateScope();
        var ambient1 = scope1.ServiceProvider.GetRequiredService<ITenantContextAccessor>();
        var ambient2 = scope2.ServiceProvider.GetRequiredService<ITenantContextAccessor>();
        ambient1.Set(account.TenantId);
        ambient2.Set(account.TenantId);
        var db1 = scope1.ServiceProvider.GetRequiredService<KadrDbContext>();
        var db2 = scope2.ServiceProvider.GetRequiredService<KadrDbContext>();

        var run1 = await db1.PayrollRuns.SingleAsync(r => r.Id == run.Id);
        var run2 = await db2.PayrollRuns.SingleAsync(r => r.Id == run.Id);
        run1.Submit();
        await db1.SaveChangesAsync();
        run2.Submit();
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => db2.SaveChangesAsync());
    }

    [Fact]
    public async Task Employee_role_forbidden_on_runs()
    {
        await factory.ResetDatabaseAsync();
        var helper = new AuthTestHelper(factory);
        var account = await helper.RegisterVerifiedAsync("Run Perm Co", "run-perm@example.com");
        var owner = helper.Authed(
            (await helper.LoginSessionAsync(account.Email, AuthTestHelper.Password, account.Slug)).AccessToken);

        var create = await owner.PostAsJsonAsync(
            BasePath,
            new CreatePayrollRunRequest(new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 30)));
        var run = await create.Content.ReadFromJsonAsync<PayrollRunDetailDto>(JsonOptions);

        await DemoteToEmployeeAsync(account.TenantId, account.Email);
        var emp = helper.Authed(
            (await helper.LoginSessionAsync(account.Email, AuthTestHelper.Password, account.Slug)).AccessToken);

        Assert.Equal(HttpStatusCode.Forbidden, (await emp.GetAsync(BasePath)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await emp.GetAsync($"{BasePath}/{run!.Id}")).StatusCode);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await emp.PostAsJsonAsync(
                BasePath,
                new CreatePayrollRunRequest(new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31)))).StatusCode);
    }

    /// <summary>
    /// Lightweight timing/smoke check (~20 employees). A 300-employee soak is optional
    /// and too heavy for the default CI suite; keep that as a manual/perf benchmark.
    /// </summary>
    [Fact]
    public async Task Calculate_succeeds_for_about_twenty_employees()
    {
        await factory.ResetDatabaseAsync();
        var helper = new AuthTestHelper(factory);
        var account = await helper.RegisterVerifiedAsync("Run Perf Co", "run-perf@example.com");
        var client = helper.Authed(
            (await helper.LoginSessionAsync(account.Email, AuthTestHelper.Password, account.Slug)).AccessToken);

        for (var i = 0; i < 20; i++)
        {
            var employeeId = await CreateEmployeeAsync(client, $"موظف {i}", $"Emp {i}");
            var salary = await client.PostAsJsonAsync(
                $"/api/v1/employees/{employeeId}/salaries",
                new CreateEmployeeSalaryRequest(
                    new DateOnly(2025, 1, 1),
                    "EGP",
                    4000m + i,
                    "Hire",
                    null));
            Assert.Equal(HttpStatusCode.OK, salary.StatusCode);
        }

        var create = await client.PostAsJsonAsync(
            BasePath,
            new CreatePayrollRunRequest(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31)));
        var run = await create.Content.ReadFromJsonAsync<PayrollRunDetailDto>(JsonOptions);
        Assert.NotNull(run);

        var calc = await client.PostAsync($"{BasePath}/{run.Id}/calculate", null);
        Assert.Equal(HttpStatusCode.OK, calc.StatusCode);
        var after = await calc.Content.ReadFromJsonAsync<PayrollRunDetailDto>(JsonOptions);
        Assert.Equal(20, after!.Headcount);
        Assert.True(after.TotalGross > 0);
    }

    private static async Task<Guid> CreateEmployeeAsync(HttpClient client, string nameAr, string nameEn)
    {
        var create = await client.PostAsJsonAsync(
            "/api/v1/employees",
            new
            {
                fullNameAr = nameAr,
                fullNameEn = nameEn,
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
