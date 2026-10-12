using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kadr.Identity.Application.Auth;
using Kadr.Identity.Domain.Roles;
using Kadr.Infrastructure.Persistence;
using Kadr.Infrastructure.Persistence.Attendance;
using Kadr.Platform.Application.Attendance;
using Kadr.Platform.Domain.Attendance;
using Kadr.Platform.Domain.Employees;
using Kadr.Platform.Domain.Settings;
using Kadr.Platform.Domain.Templates;
using Kadr.SharedKernel.Constants;
using Kadr.SharedKernel.Tenancy;
using Kadr.TenantIsolation.Tests.Auth;
using Kadr.TenantIsolation.Tests.Register;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kadr.TenantIsolation.Tests.Attendance;

[Collection(RegisterCollection.Name)]
public sealed class ShiftAssignmentRuleApiTests(RegisterApiFactory factory)
{
    private static readonly JsonSerializerOptions JsonOptions = AuthTestHelper.JsonOptions;

    [Fact]
    public async Task Precedence_day_over_employee_over_job_title_over_dept_over_branch_over_default()
    {
        await factory.ResetDatabaseAsync();
        var helper = new AuthTestHelper(factory);
        var account = await helper.RegisterVerifiedAsync("Rules Prec Co", "rules-prec@example.com");
        var client = helper.Authed((await helper.LoginSessionAsync(account.Email, AuthTestHelper.Password, account.Slug)).AccessToken);

        var workDate = new DateOnly(2026, 10, 12);
        Guid empId = default;
        Guid defaultShift = default;
        Guid branchShift = default;
        Guid deptShift = default;
        Guid jobShift = default;
        Guid empShift = default;
        Guid dayShift = default;
        Guid branchId = default;
        Guid deptId = default;
        Guid jobId = default;

        await helper.WithTenantAsync(account.TenantId, async db =>
        {
            var legal = await db.LegalEntities.FirstAsync();
            branchId = (await db.Branches.FirstAsync()).Id;
            defaultShift = (await db.Shifts.AsNoTracking().FirstAsync(s => s.IsActive)).Id;

            db.Shifts.AddRange(
                Shift.CreateCustom(account.TenantId, "فرع", "Branch", new TimeOnly(8, 0), new TimeOnly(16, 0), 10),
                Shift.CreateCustom(account.TenantId, "قسم", "Dept", new TimeOnly(9, 0), new TimeOnly(17, 0), 10),
                Shift.CreateCustom(account.TenantId, "مسمى", "Job", new TimeOnly(10, 0), new TimeOnly(18, 0), 10),
                Shift.CreateCustom(account.TenantId, "موظف", "Emp", new TimeOnly(7, 0), new TimeOnly(15, 0), 10),
                Shift.CreateCustom(account.TenantId, "يوم", "Day", new TimeOnly(6, 0), new TimeOnly(14, 0), 10));

            var dept = Department.CreateCustom(account.TenantId, "قسم أ", "Dept A");
            var job = JobTitle.CreateCustom(account.TenantId, "مطور", "Developer");
            db.Departments.Add(dept);
            db.JobTitles.Add(job);
            await db.SaveChangesAsync();

            branchShift = await db.Shifts.Where(s => s.NameEn == "Branch").Select(s => s.Id).SingleAsync();
            deptShift = await db.Shifts.Where(s => s.NameEn == "Dept").Select(s => s.Id).SingleAsync();
            jobShift = await db.Shifts.Where(s => s.NameEn == "Job").Select(s => s.Id).SingleAsync();
            empShift = await db.Shifts.Where(s => s.NameEn == "Emp").Select(s => s.Id).SingleAsync();
            dayShift = await db.Shifts.Where(s => s.NameEn == "Day").Select(s => s.Id).SingleAsync();
            deptId = dept.Id;
            jobId = job.Id;

            var hire = new DateOnly(2024, 1, 1);
            var emp = Employee.Hire(
                account.TenantId, legal.Id, branchId, "R-1", "موظف قواعد", "Rules Emp", hire, EmployeeStatus.Active, ContractType.Unlimited);
            emp.UpdateJob(branchId, deptId, jobId, null, hire, null, ContractType.Unlimited, null, null);
            db.Employees.Add(emp);

            var existing = await db.TenantSettings.SingleOrDefaultAsync(s => s.Key == SettingKeys.AttendanceDefaultShiftId);
            if (existing is null)
            {
                db.TenantSettings.Add(TenantSetting.Create(
                    account.TenantId,
                    SettingKeys.AttendanceDefaultShiftId,
                    JsonSerializer.Serialize(defaultShift)));
            }
            else
            {
                existing.SetValue(JsonSerializer.Serialize(defaultShift));
            }

            await db.SaveChangesAsync();
            empId = emp.Id;
            return 0;
        });

        async Task AssertResolved(Guid? expectedShift, string expectedSource)
        {
            var effective = await helper.WithTenantAsync(account.TenantId, db =>
                ShiftRuleResolver.ResolveAsync(db, empId, workDate, defaultShift, CancellationToken.None));
            Assert.Equal(expectedShift, effective.ShiftId);
            Assert.Equal(expectedSource, effective.Source);
        }

        await AssertResolved(defaultShift, "default");

        await PostRule(client, "Branch", [branchId], branchShift, workDate);
        await AssertResolved(branchShift, nameof(ShiftAssignmentTargetType.Branch));

        await PostRule(client, "Department", [deptId], deptShift, workDate);
        await AssertResolved(deptShift, nameof(ShiftAssignmentTargetType.Department));

        await PostRule(client, "JobTitle", [jobId], jobShift, workDate);
        await AssertResolved(jobShift, nameof(ShiftAssignmentTargetType.JobTitle));

        await PostRule(client, "Employee", [empId], empShift, workDate);
        await AssertResolved(empShift, nameof(ShiftAssignmentTargetType.Employee));

        await helper.WithTenantAsync(account.TenantId, async db =>
        {
            db.ShiftAssignments.Add(ShiftAssignment.Create(account.TenantId, empId, workDate, dayShift, false));
            await db.SaveChangesAsync();
            return 0;
        });
        await AssertResolved(dayShift, "day");
    }

    [Fact]
    public async Task Effective_window_and_department_transfer_pick_new_rule()
    {
        await factory.ResetDatabaseAsync();
        var helper = new AuthTestHelper(factory);
        var account = await helper.RegisterVerifiedAsync("Rules Xfer Co", "rules-xfer@example.com");
        var client = helper.Authed((await helper.LoginSessionAsync(account.Email, AuthTestHelper.Password, account.Slug)).AccessToken);

        var from = new DateOnly(2026, 10, 1);
        var mid = new DateOnly(2026, 10, 15);
        Guid empId = default;
        Guid deptA = default;
        Guid deptB = default;
        Guid shiftA = default;
        Guid shiftB = default;
        Guid branchId = default;

        await helper.WithTenantAsync(account.TenantId, async db =>
        {
            var legal = await db.LegalEntities.FirstAsync();
            branchId = (await db.Branches.FirstAsync()).Id;
            db.Shifts.Add(Shift.CreateCustom(account.TenantId, "أ", "ShiftA", new TimeOnly(8, 0), new TimeOnly(16, 0), 5));
            db.Shifts.Add(Shift.CreateCustom(account.TenantId, "ب", "ShiftB", new TimeOnly(9, 0), new TimeOnly(17, 0), 5));
            var dA = Department.CreateCustom(account.TenantId, "قسم أ", "DeptA");
            var dB = Department.CreateCustom(account.TenantId, "قسم ب", "DeptB");
            db.Departments.AddRange(dA, dB);
            await db.SaveChangesAsync();
            shiftA = await db.Shifts.Where(s => s.NameEn == "ShiftA").Select(s => s.Id).SingleAsync();
            shiftB = await db.Shifts.Where(s => s.NameEn == "ShiftB").Select(s => s.Id).SingleAsync();
            deptA = dA.Id;
            deptB = dB.Id;

            var hire = new DateOnly(2024, 1, 1);
            var emp = Employee.Hire(
                account.TenantId, legal.Id, branchId, "X-1", "نقل", "Xfer", hire, EmployeeStatus.Active, ContractType.Unlimited);
            emp.UpdateJob(branchId, deptA, null, null, hire, null, ContractType.Unlimited, null, null);
            db.Employees.Add(emp);
            await db.SaveChangesAsync();
            empId = emp.Id;
            return 0;
        });

        await PostRule(client, "Department", [deptA], shiftA, from, effectiveTo: mid);
        await PostRule(client, "Department", [deptB], shiftB, from);

        var dbRule = await helper.WithTenantAsync(account.TenantId, db =>
            db.ShiftAssignmentRules.AsNoTracking()
                .Where(r => r.TargetId == deptA)
                .Select(r => new { r.TargetType, r.TargetId, r.ShiftId, r.EffectiveFrom, r.EffectiveTo })
                .SingleAsync());
        Assert.Equal(ShiftAssignmentTargetType.Department, dbRule.TargetType);
        Assert.Equal(shiftA, dbRule.ShiftId);
        Assert.Equal(from, dbRule.EffectiveFrom);
        Assert.Equal(mid, dbRule.EffectiveTo);

        var empDept = await helper.WithTenantAsync(account.TenantId, db =>
            db.Employees.AsNoTracking().Where(e => e.Id == empId).Select(e => e.DepartmentId).SingleAsync());
        Assert.Equal(deptA, empDept);

        var before = await helper.WithTenantAsync(account.TenantId, db =>
            ShiftRuleResolver.ResolveAsync(db, empId, mid.AddDays(-1), null, CancellationToken.None));
        Assert.Equal(shiftA, before.ShiftId);

        var afterWindow = await helper.WithTenantAsync(account.TenantId, db =>
            ShiftRuleResolver.ResolveAsync(db, empId, mid.AddDays(1), null, CancellationToken.None));
        Assert.Null(afterWindow.ShiftId);

        await helper.WithTenantAsync(account.TenantId, async db =>
        {
            var emp = await db.Employees.SingleAsync(e => e.Id == empId);
            emp.UpdateJob(branchId, deptB, null, null, new DateOnly(2024, 1, 1), null, ContractType.Unlimited, null, null);
            await db.SaveChangesAsync();
            return 0;
        });

        var afterTransfer = await helper.WithTenantAsync(account.TenantId, db =>
            ShiftRuleResolver.ResolveAsync(db, empId, mid.AddDays(1), null, CancellationToken.None));
        Assert.Equal(shiftB, afterTransfer.ShiftId);
    }

    [Fact]
    public async Task Overlap_same_level_is_rejected_preview_and_isolation_and_permission()
    {
        await factory.ResetDatabaseAsync();
        var helper = new AuthTestHelper(factory);
        var a = await helper.RegisterVerifiedAsync("Rules Iso A", "rules-iso-a@example.com");
        var b = await helper.RegisterVerifiedAsync("Rules Iso B", "rules-iso-b@example.com");
        var clientA = helper.Authed((await helper.LoginSessionAsync(a.Email, AuthTestHelper.Password, a.Slug)).AccessToken);
        var clientB = helper.Authed((await helper.LoginSessionAsync(b.Email, AuthTestHelper.Password, b.Slug)).AccessToken);

        Guid empA = default;
        Guid shiftA = default;
        await helper.WithTenantAsync(a.TenantId, async db =>
        {
            var legal = await db.LegalEntities.FirstAsync();
            var branchId = (await db.Branches.FirstAsync()).Id;
            shiftA = (await db.Shifts.AsNoTracking().FirstAsync()).Id;
            var hire = new DateOnly(2024, 1, 1);
            var emp = Employee.Hire(
                a.TenantId, legal.Id, branchId, "ISO-1", "عزل", "Iso", hire, EmployeeStatus.Active, ContractType.Unlimited);
            db.Employees.Add(emp);
            await db.SaveChangesAsync();
            empA = emp.Id;
            return 0;
        });

        var from = new DateOnly(2026, 10, 1);
        var created = await PostRule(clientA, "Employee", [empA], shiftA, from);
        Assert.Single(created);

        var overlap = await clientA.PostAsJsonAsync(
            "/api/v1/attendance/shift-rules",
            new CreateShiftAssignmentRulesRequest(
                "Employee", [empA], shiftA, false, from, null, true));
        Assert.Equal(HttpStatusCode.Conflict, overlap.StatusCode);
        Assert.Equal(ErrorCodes.AttendanceShiftRuleOverlap, await AuthTestHelper.ReadErrorCodeAsync(overlap));

        var preview = await clientA.PostAsJsonAsync(
            "/api/v1/attendance/shift-rules/preview",
            new PreviewShiftAssignmentRulesRequest("Employee", [empA], from, null));
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        var previewDto = (await preview.Content.ReadFromJsonAsync<PreviewShiftAssignmentRulesDto>(JsonOptions))!;
        Assert.Equal(1, previewDto.AffectedEmployeeCount);

        var listB = await clientB.GetFromJsonAsync<PagedShiftAssignmentRulesDto>(
            "/api/v1/attendance/shift-rules?page=1&pageSize=20",
            JsonOptions);
        Assert.NotNull(listB);
        Assert.Empty(listB.Items);

        var cross = await clientB.PutAsJsonAsync(
            $"/api/v1/attendance/shift-rules/{created[0].Id}",
            new UpdateShiftAssignmentRuleRequest(shiftA, false, from, null, true));
        Assert.Equal(HttpStatusCode.NotFound, cross.StatusCode);

        await helper.WithTenantAsync(a.TenantId, async db =>
        {
            var user = await db.Users.SingleAsync(u => u.Email == a.Email.ToLowerInvariant());
            var employeeRole = await db.Roles.SingleAsync(r =>
                r.TenantId == a.TenantId && r.Name == SystemRoleNames.Employee);
            var ownerLink = await db.UserRoles.SingleAsync(ur => ur.UserId == user.Id);
            db.UserRoles.Remove(ownerLink);
            db.UserRoles.Add(UserRole.Create(a.TenantId, user.Id, employeeRole.Id));
            await db.SaveChangesAsync();
            return 0;
        });

        using (var scope = factory.Services.CreateScope())
        {
            var userId = await helper.WithTenantAsync(a.TenantId, db =>
                db.Users.AsNoTracking().Where(u => u.Email == a.Email.ToLowerInvariant()).Select(u => u.Id).SingleAsync());
            scope.ServiceProvider.GetRequiredService<IPermissionCacheInvalidator>().Invalidate(a.TenantId, userId);
        }

        var empToken = (await helper.LoginSessionAsync(a.Email, AuthTestHelper.Password, a.Slug)).AccessToken;
        var empClient = helper.Authed(empToken);
        var forbidden = await empClient.GetAsync("/api/v1/attendance/shift-rules");
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        var anonymous = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/v1/attendance/shift-rules")).StatusCode);
    }

    [Fact]
    public async Task ResolveMany_stays_within_query_budget_including_large_seed()
    {
        await factory.ResetDatabaseAsync();
        var helper = new AuthTestHelper(factory);
        var account = await helper.RegisterVerifiedAsync("Rules Qc Co", "rules-qc@example.com");

        Guid shiftId = default;
        Guid branchId = default;
        var employeeIds = new List<Guid>();

        await helper.WithTenantAsync(account.TenantId, async db =>
        {
            var legal = await db.LegalEntities.FirstAsync();
            branchId = (await db.Branches.FirstAsync()).Id;
            shiftId = (await db.Shifts.AsNoTracking().FirstAsync()).Id;
            var dept = Department.CreateCustom(account.TenantId, "قسم كبير", "BigDept");
            db.Departments.Add(dept);
            await db.SaveChangesAsync();

            db.ShiftAssignmentRules.Add(ShiftAssignmentRule.Create(
                account.TenantId,
                ShiftAssignmentTargetType.Department,
                dept.Id,
                shiftId,
                false,
                new DateOnly(2026, 1, 1),
                null,
                null));

            const int seed = 10_000;
            var hire = new DateOnly(2024, 1, 1);
            var batch = new List<Employee>(1000);
            for (var i = 0; i < seed; i++)
            {
                var emp = Employee.Hire(
                    account.TenantId,
                    legal.Id,
                    branchId,
                    $"QC-{i:D5}",
                    $"موظف {i}",
                    $"Emp {i}",
                    hire,
                    EmployeeStatus.Active,
                    ContractType.Unlimited);
                emp.UpdateJob(branchId, dept.Id, null, null, hire, null, ContractType.Unlimited, null, null);
                batch.Add(emp);
                if (batch.Count >= 1000)
                {
                    db.Employees.AddRange(batch);
                    await db.SaveChangesAsync();
                    employeeIds.AddRange(batch.Select(e => e.Id));
                    batch.Clear();
                    db.ChangeTracker.Clear();
                }
            }

            if (batch.Count > 0)
            {
                db.Employees.AddRange(batch);
                await db.SaveChangesAsync();
                employeeIds.AddRange(batch.Select(e => e.Id));
            }

            return 0;
        });

        var workDate = new DateOnly(2026, 10, 12);
        var sample = employeeIds.Take(200).ToList();
        var counter = new QueryCounter();
        using var subscription = DiagnosticListener.AllListeners.Subscribe(new AllListenersObserver(counter));

        await helper.WithTenantAsync(account.TenantId, async db =>
        {
            var map = await ShiftRuleResolver.ResolveManyAsync(db, sample, workDate, null, CancellationToken.None);
            Assert.Equal(sample.Count, map.Count);
            Assert.All(map.Values, v => Assert.Equal(shiftId, v.ShiftId));
            return 0;
        });

        Assert.True(counter.Count <= 8, $"Expected ≤ 8 queries for ResolveMany, got {counter.Count}");
        GC.KeepAlive(subscription);

        await helper.WithTenantAsync(account.TenantId, async db =>
        {
            var map = await ShiftRuleResolver.ResolveManyAsync(
                db, employeeIds, workDate, null, CancellationToken.None);
            Assert.Equal(10_000, map.Count);
            Assert.Equal(shiftId, map[employeeIds[0]].ShiftId);
            Assert.Equal(shiftId, map[employeeIds[^1]].ShiftId);
            return 0;
        });
    }

    [Fact]
    public async Task Backdate_requires_confirmRecalc()
    {
        await factory.ResetDatabaseAsync();
        var helper = new AuthTestHelper(factory);
        var account = await helper.RegisterVerifiedAsync("Rules Back Co", "rules-back@example.com");
        var client = helper.Authed((await helper.LoginSessionAsync(account.Email, AuthTestHelper.Password, account.Slug)).AccessToken);

        Guid empId = default;
        Guid shiftId = default;
        await helper.WithTenantAsync(account.TenantId, async db =>
        {
            var legal = await db.LegalEntities.FirstAsync();
            var branchId = (await db.Branches.FirstAsync()).Id;
            shiftId = (await db.Shifts.AsNoTracking().FirstAsync()).Id;
            var hire = new DateOnly(2024, 1, 1);
            var emp = Employee.Hire(
                account.TenantId, legal.Id, branchId, "B-1", "سابق", "Back", hire, EmployeeStatus.Active, ContractType.Unlimited);
            db.Employees.Add(emp);
            await db.SaveChangesAsync();
            empId = emp.Id;
            return 0;
        });

        var past = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(-10));
        var denied = await client.PostAsJsonAsync(
            "/api/v1/attendance/shift-rules",
            new CreateShiftAssignmentRulesRequest(
                "Employee", [empId], shiftId, false, past, null, false));
        Assert.Equal(HttpStatusCode.BadRequest, denied.StatusCode);
        Assert.Equal(
            ErrorCodes.AttendanceShiftRuleBackdateRequiresConfirm,
            await AuthTestHelper.ReadErrorCodeAsync(denied));

        var ok = await PostRule(client, "Employee", [empId], shiftId, past, confirmRecalc: true);
        Assert.Single(ok);
    }

    private static async Task<List<ShiftAssignmentRuleDto>> PostRule(
        HttpClient client,
        string targetType,
        Guid[] targetIds,
        Guid shiftId,
        DateOnly effectiveFrom,
        DateOnly? effectiveTo = null,
        bool confirmRecalc = true)
    {
        var response = await client.PostAsJsonAsync(
            "/api/v1/attendance/shift-rules",
            new CreateShiftAssignmentRulesRequest(
                targetType,
                targetIds,
                shiftId,
                IsDayOff: false,
                effectiveFrom,
                effectiveTo,
                confirmRecalc));
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, body);
        return JsonSerializer.Deserialize<List<ShiftAssignmentRuleDto>>(body, JsonOptions)!;
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

        public void OnNext(DiagnosticListener value)
        {
            if (value.Name == "Microsoft.EntityFrameworkCore")
            {
                value.Subscribe(new QueryObserver(counter));
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
            if (value.Key == "Microsoft.EntityFrameworkCore.Database.Command.CommandExecuted")
            {
                Interlocked.Increment(ref counter.Count);
            }
        }
    }
}
