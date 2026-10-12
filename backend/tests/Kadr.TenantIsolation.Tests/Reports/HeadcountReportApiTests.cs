using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Kadr.Identity.Application.Auth;
using Kadr.Identity.Domain.Roles;
using Kadr.Infrastructure.Persistence;
using Kadr.Platform.Application.Abstractions;
using Kadr.Platform.Application.Reports;
using Kadr.Platform.Domain.Employees;
using Kadr.Platform.Domain.Org;
using Kadr.Platform.Domain.Templates;
using Kadr.SharedKernel.Constants;
using Kadr.SharedKernel.Tenancy;
using Kadr.TenantIsolation.Tests.Auth;
using Kadr.TenantIsolation.Tests.Register;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Kadr.TenantIsolation.Tests.Reports;

[Collection(RegisterCollection.Name)]
public sealed class HeadcountReportApiTests(RegisterApiFactory factory)
{
    private static readonly JsonSerializerOptions JsonOptions = AuthTestHelper.JsonOptions;

    [Fact]
    public async Task Employee_role_cannot_open_reports_hub()
    {
        await factory.ResetDatabaseAsync();
        var helper = new AuthTestHelper(factory);
        var account = await helper.RegisterVerifiedAsync("Reports Emp Co", "reports-emp@example.com");
        await helper.WithTenantAsync(account.TenantId, async db =>
        {
            var user = await db.Users.SingleAsync(u => u.Email == account.Email.ToLowerInvariant());
            var employeeRole = await db.Roles.SingleAsync(r =>
                r.TenantId == account.TenantId && r.Name == SystemRoleNames.Employee);
            var link = await db.UserRoles.SingleAsync(ur => ur.UserId == user.Id);
            db.UserRoles.Remove(link);
            db.UserRoles.Add(UserRole.Create(account.TenantId, user.Id, employeeRole.Id));
            await db.SaveChangesAsync();
            factory.Services.CreateScope().ServiceProvider
                .GetRequiredService<IPermissionCacheInvalidator>()
                .Invalidate(account.TenantId, user.Id);
            return 0;
        });

        var session = await helper.LoginSessionAsync(account.Email, AuthTestHelper.Password, account.Slug);
        var client = helper.Authed(session.AccessToken);
        var response = await client.GetAsync("/api/v1/reports");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Headcount_is_tenant_isolated()
    {
        await factory.ResetDatabaseAsync();
        var helper = new AuthTestHelper(factory);
        var a = await helper.RegisterVerifiedAsync("Reports Iso A", "reports-iso-a@example.com");
        var b = await helper.RegisterVerifiedAsync("Reports Iso B", "reports-iso-b@example.com");
        await SeedEmployeesAsync(a.TenantId, ("EMP-A1", "موظف أ", EmployeeStatus.Active, null, null));
        await SeedEmployeesAsync(b.TenantId, ("EMP-B1", "موظف ب", EmployeeStatus.Active, null, null));

        var sessionA = await helper.LoginSessionAsync(a.Email, AuthTestHelper.Password, a.Slug);
        var clientA = helper.Authed(sessionA.AccessToken);
        var report = await clientA.GetFromJsonAsync<HeadcountReportDto>(
            "/api/v1/reports/headcount?page=1&pageSize=50",
            JsonOptions);
        Assert.NotNull(report);
        Assert.DoesNotContain(report.Items, r => r.EmployeeNumber.StartsWith("EMP-B", StringComparison.Ordinal));
        Assert.Contains(report.Items, r => r.EmployeeNumber == "EMP-A1");
    }

    [Fact]
    public async Task Headcount_as_of_excludes_terminated_by_contract_end()
    {
        await factory.ResetDatabaseAsync();
        var helper = new AuthTestHelper(factory);
        var account = await helper.RegisterVerifiedAsync("Reports AsOf Co", "reports-asof@example.com");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await SeedEmployeesAsync(
            account.TenantId,
            ("EMP-ACT", "نشط", EmployeeStatus.Active, null, null),
            ("EMP-LEFT", "منتهٍ", EmployeeStatus.Terminated, today.AddDays(-1), null));

        var session = await helper.LoginSessionAsync(account.Email, AuthTestHelper.Password, account.Slug);
        var client = helper.Authed(session.AccessToken);
        var response = await client.GetAsync(
            $"/api/v1/reports/headcount?asOf={today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}&page=1&pageSize=50");
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var report = JsonSerializer.Deserialize<HeadcountReportDto>(body, JsonOptions);
        Assert.NotNull(report);
        Assert.True(report.Summary.Total >= 1, body);
        Assert.Contains(report.Items, r => r.EmployeeNumber == "EMP-ACT");
        Assert.DoesNotContain(report.Items, r => r.EmployeeNumber == "EMP-LEFT");
    }

    [Fact]
    public async Task Manager_team_scope_limits_headcount_tiles_and_export()
    {
        await factory.ResetDatabaseAsync();
        var helper = new AuthTestHelper(factory);
        var account = await helper.RegisterVerifiedAsync("Reports Scope Co", "reports-scope@example.com");

        Guid managerUserId = default;
        Guid managerEmpId = default;
        Guid reportEmpId = default;
        Guid outsiderEmpId = default;

        await helper.WithTenantAsync(account.TenantId, async db =>
        {
            var branch = await db.Branches.AsNoTracking().FirstAsync();
            var legal = await db.LegalEntities.AsNoTracking().FirstAsync();
            var hire = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-1));

            var manager = Employee.Hire(
                account.TenantId, legal.Id, branch.Id, "MGR-1", "المدير", "Manager",
                hire, EmployeeStatus.Active, ContractType.Unlimited);
            var report = Employee.Hire(
                account.TenantId, legal.Id, branch.Id, "REP-1", "تابع", "Report",
                hire, EmployeeStatus.Active, ContractType.Unlimited);
            var outsider = Employee.Hire(
                account.TenantId, legal.Id, branch.Id, "OUT-1", "خارج", "Outsider",
                hire, EmployeeStatus.Active, ContractType.Unlimited);
            report.UpdateJob(branch.Id, null, null, manager.Id, hire, null, ContractType.Unlimited, null, null);

            db.Employees.AddRange(manager, report, outsider);
            await db.SaveChangesAsync();

            var user = await db.Users.SingleAsync(u => u.Email == account.Email.ToLowerInvariant());
            var managerRole = await db.Roles.SingleAsync(r =>
                r.TenantId == account.TenantId && r.Name == SystemRoleNames.Manager);
            var link = await db.UserRoles.SingleAsync(ur => ur.UserId == user.Id);
            db.UserRoles.Remove(link);
            db.UserRoles.Add(UserRole.Create(account.TenantId, user.Id, managerRole.Id, DataScope.Team));
            user.SetEmployeeId(manager.Id);
            manager.LinkUser(user.Id);
            await db.SaveChangesAsync();

            managerUserId = user.Id;
            managerEmpId = manager.Id;
            reportEmpId = report.Id;
            outsiderEmpId = outsider.Id;
            return 0;
        });

        using (var scope = factory.Services.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<IPermissionCacheInvalidator>()
                .Invalidate(account.TenantId, managerUserId);
        }

        var session = await helper.LoginSessionAsync(account.Email, AuthTestHelper.Password, account.Slug);
        var client = helper.Authed(session.AccessToken);
        var headcount = await client.GetFromJsonAsync<HeadcountReportDto>(
            "/api/v1/reports/headcount?page=1&pageSize=50",
            JsonOptions);
        Assert.NotNull(headcount);
        Assert.Equal(2, headcount.Summary.Total);
        Assert.Contains(headcount.Items, r => r.EmployeeId == managerEmpId);
        Assert.Contains(headcount.Items, r => r.EmployeeId == reportEmpId);
        Assert.DoesNotContain(headcount.Items, r => r.EmployeeId == outsiderEmpId);

        client.DefaultRequestHeaders.AcceptLanguage.Clear();
        client.DefaultRequestHeaders.AcceptLanguage.Add(new StringWithQualityHeaderValue("en"));
        var xlsx = await client.GetAsync("/api/v1/reports/headcount/export?format=xlsx");
        Assert.Equal(HttpStatusCode.Forbidden, xlsx.StatusCode);

        await helper.WithTenantAsync(account.TenantId, async db =>
        {
            var user = await db.Users.SingleAsync(u => u.Id == managerUserId);
            var ownerRole = await db.Roles.SingleAsync(r =>
                r.TenantId == account.TenantId && r.Name == SystemRoleNames.Owner);
            var link = await db.UserRoles.SingleAsync(ur => ur.UserId == user.Id);
            db.UserRoles.Remove(link);
            db.UserRoles.Add(UserRole.Create(account.TenantId, user.Id, ownerRole.Id, DataScope.Team));
            await db.SaveChangesAsync();
            return 0;
        });
        using (var scope = factory.Services.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<IPermissionCacheInvalidator>()
                .Invalidate(account.TenantId, managerUserId);
        }

        session = await helper.LoginSessionAsync(account.Email, AuthTestHelper.Password, account.Slug);
        client = helper.Authed(session.AccessToken);
        client.DefaultRequestHeaders.AcceptLanguage.Add(new StringWithQualityHeaderValue("ar"));
        var export = await client.GetAsync("/api/v1/reports/headcount/export?format=pdf");
        Assert.Equal(HttpStatusCode.OK, export.StatusCode);
        Assert.Equal("application/pdf", export.Content.Headers.ContentType?.MediaType);
        var bytes = await export.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.Length > 100);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(bytes.AsSpan(0, 4)));

        var audits = await helper.CountAuditAsync(account.TenantId, AuditActions.ReportExported);
        Assert.True(audits >= 1);
    }

    [Fact]
    public async Task Headcount_list_stays_within_query_budget()
    {
        await factory.ResetDatabaseAsync();
        var helper = new AuthTestHelper(factory);
        var account = await helper.RegisterVerifiedAsync("Reports Qc Co", "reports-qc@example.com");
        await SeedEmployeesAsync(
            account.TenantId,
            ("EMP-Q1", "واحد", EmployeeStatus.Active, null, null),
            ("EMP-Q2", "اثنان", EmployeeStatus.Probation, null, null));

        var userId = await helper.WithTenantAsync(account.TenantId, db =>
            db.Users.AsNoTracking().Where(u => u.Email == account.Email.ToLowerInvariant()).Select(u => u.Id).SingleAsync());

        var counter = new QueryCounter();
        using var subscription = DiagnosticListener.AllListeners.Subscribe(new AllListenersObserver(counter));

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KadrDbContext>();
        scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>().Set(account.TenantId, userId);
        var result = await scope.ServiceProvider.GetRequiredService<IHeadcountReportService>()
            .GetHeadcountAsync(new HeadcountReportQuery(null, null, null, null, null, 1, 20, null, false));
        Assert.True(result.IsSuccess);
        Assert.True(counter.Count <= 6, $"Expected ≤ 6 queries, got {counter.Count}");
        Assert.Empty(db.ChangeTracker.Entries());
        GC.KeepAlive(subscription);
    }

    [Fact]
    public async Task Data_scopes_own_department_branch_and_all()
    {
        await factory.ResetDatabaseAsync();
        var helper = new AuthTestHelper(factory);
        var account = await helper.RegisterVerifiedAsync("Reports Scopes Co", "reports-scopes@example.com");

        Guid userId = default;
        Guid ownEmp = default;
        Guid deptEmp = default;
        Guid branchEmp = default;
        Guid otherBranchEmp = default;
        Guid deptId = default;
        Guid branchId = default;
        Guid otherBranchId = default;

        await helper.WithTenantAsync(account.TenantId, async db =>
        {
            var legal = await db.LegalEntities.FirstAsync();
            var branch = await db.Branches.FirstAsync();
            branchId = branch.Id;
            var other = Branch.Create(
                account.TenantId, legal.Id, "فرع آخر", "Other Branch", null);
            db.Branches.Add(other);
            await db.SaveChangesAsync();
            otherBranchId = other.Id;

            var dept = Department.CreateCustom(account.TenantId, "قسم أ", "Dept A");
            db.Departments.Add(dept);
            await db.SaveChangesAsync();
            deptId = dept.Id;

            var hire = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-6));
            var me = Employee.Hire(account.TenantId, legal.Id, branchId, "OWN-1", "أنا", "Me", hire, EmployeeStatus.Active, ContractType.Unlimited);
            me.UpdateJob(branchId, deptId, null, null, hire, null, ContractType.Unlimited, null, null);
            var sameDept = Employee.Hire(account.TenantId, legal.Id, branchId, "DEP-1", "نفس القسم", "DeptMate", hire, EmployeeStatus.Active, ContractType.Unlimited);
            sameDept.UpdateJob(branchId, deptId, null, null, hire, null, ContractType.Unlimited, null, null);
            var sameBranch = Employee.Hire(account.TenantId, legal.Id, branchId, "BR-1", "نفس الفرع", "BranchMate", hire, EmployeeStatus.Active, ContractType.FixedTerm);
            var otherBr = Employee.Hire(account.TenantId, legal.Id, otherBranchId, "OB-1", "فرع آخر", "OtherBr", hire, EmployeeStatus.Active, ContractType.Unlimited);
            db.Employees.AddRange(me, sameDept, sameBranch, otherBr);
            await db.SaveChangesAsync();

            var user = await db.Users.SingleAsync(u => u.Email == account.Email.ToLowerInvariant());
            user.SetEmployeeId(me.Id);
            me.LinkUser(user.Id);
            await db.SaveChangesAsync();

            userId = user.Id;
            ownEmp = me.Id;
            deptEmp = sameDept.Id;
            branchEmp = sameBranch.Id;
            otherBranchEmp = otherBr.Id;
            return 0;
        });

        async Task<int> CountForScope(DataScope scope)
        {
            await helper.WithTenantAsync(account.TenantId, async db =>
            {
                var link = await db.UserRoles.SingleAsync(ur => ur.UserId == userId);
                link.ReplaceRole(link.RoleId, scope);
                await db.SaveChangesAsync();
                return 0;
            });
            using (var s = factory.Services.CreateScope())
            {
                s.ServiceProvider.GetRequiredService<IPermissionCacheInvalidator>().Invalidate(account.TenantId, userId);
            }

            var session = await helper.LoginSessionAsync(account.Email, AuthTestHelper.Password, account.Slug);
            var client = helper.Authed(session.AccessToken);
            var report = await client.GetFromJsonAsync<HeadcountReportDto>(
                "/api/v1/reports/headcount?page=1&pageSize=50",
                JsonOptions);
            return report!.Summary.Total;
        }

        Assert.Equal(1, await CountForScope(DataScope.Own));
        Assert.Equal(2, await CountForScope(DataScope.Department));
        Assert.Equal(3, await CountForScope(DataScope.Branch));
        Assert.True(await CountForScope(DataScope.All) >= 4);

        _ = ownEmp;
        _ = deptEmp;
        _ = branchEmp;
        _ = otherBranchEmp;
        _ = deptId;
        _ = branchId;
        _ = otherBranchId;
    }

    private async Task SeedEmployeesAsync(
        Guid tenantId,
        params (string Number, string NameAr, EmployeeStatus Status, DateOnly? ContractEnd, Guid? ManagerId)[] rows)
    {
        var helper = new AuthTestHelper(factory);
        await helper.WithTenantAsync(tenantId, async db =>
        {
            var branch = await db.Branches.AsNoTracking().FirstAsync();
            var legal = await db.LegalEntities.AsNoTracking().FirstAsync();
            var hire = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-1));
            foreach (var row in rows)
            {
                var emp = Employee.Hire(
                    tenantId,
                    legal.Id,
                    branch.Id,
                    row.Number,
                    row.NameAr,
                    null,
                    hire,
                    row.Status,
                    ContractType.Unlimited);
                emp.UpdateJob(
                    branch.Id,
                    null,
                    null,
                    row.ManagerId,
                    hire,
                    null,
                    ContractType.Unlimited,
                    null,
                    row.ContractEnd);
                if (row.Status == EmployeeStatus.Terminated)
                {
                    emp.SetStatus(EmployeeStatus.Terminated);
                }

                db.Employees.Add(emp);
            }

            await db.SaveChangesAsync();
            return 0;
        });
    }

    private sealed class QueryCounter
    {
        public int Count;
    }

    private sealed class AllListenersObserver(QueryCounter counter) : IObserver<DiagnosticListener>
    {
        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
        }

        public void OnNext(DiagnosticListener listener)
        {
            if (listener.Name == DbLoggerCategory.Name || listener.Name.Contains("EntityFramework", StringComparison.Ordinal))
            {
                listener.Subscribe(new QueryObserver(counter));
            }
        }
    }

    private sealed class QueryObserver(QueryCounter counter) : IObserver<KeyValuePair<string, object?>>
    {
        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
        }

        public void OnNext(KeyValuePair<string, object?> value)
        {
            if (value.Key == RelationalEventId.CommandExecuted.Name)
            {
                Interlocked.Increment(ref counter.Count);
            }
        }
    }
}
