using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kadr.Platform.Application.Attendance;
using Kadr.Platform.Application.Employees;
using Kadr.SharedKernel.Constants;
using Kadr.TenantIsolation.Tests.Auth;
using Kadr.TenantIsolation.Tests.Register;
using Microsoft.EntityFrameworkCore;

namespace Kadr.TenantIsolation.Tests.Attendance;

[Collection(RegisterCollection.Name)]
public sealed class AttendanceApiTests(RegisterApiFactory factory)
{
    private static readonly JsonSerializerOptions JsonOptions = AuthTestHelper.JsonOptions;

    // Near Cairo — used for branch + punch
    private const double Lat = 30.0444;
    private const double Lon = 31.2357;

    [Fact]
    public async Task Punch_and_settings_are_tenant_isolated()
    {
        await factory.ResetDatabaseAsync();
        var helper = new AuthTestHelper(factory);
        var a = await helper.RegisterVerifiedAsync("Att Co A", "att-owner-a@example.com");
        var b = await helper.RegisterVerifiedAsync("Att Co B", "att-owner-b@example.com");

        var sessionA = await helper.LoginSessionAsync(a.Email, AuthTestHelper.Password, a.Slug);
        var sessionB = await helper.LoginSessionAsync(b.Email, AuthTestHelper.Password, b.Slug);
        var clientA = helper.Authed(sessionA.AccessToken);
        var clientB = helper.Authed(sessionB.AccessToken);

        var empA = await CreateLinkedEmployeeAsync(helper, clientA, a.TenantId, a.Email, "موظف أ", "Employee A");
        await CreateLinkedEmployeeAsync(helper, clientB, b.TenantId, b.Email, "موظف ب", "Employee B");

        // Configure branch location + default shift for A
        var branchesA = await clientA.GetFromJsonAsync<List<BranchLocationDto>>(
            "/api/v1/attendance/settings/branches",
            JsonOptions);
        Assert.NotNull(branchesA);
        Assert.NotEmpty(branchesA);
        var branchId = branchesA[0].BranchId;

        var loc = await clientA.PutAsJsonAsync(
            $"/api/v1/attendance/settings/branches/{branchId}/location",
            new
            {
                latitude = Lat,
                longitude = Lon,
                geofenceRadiusMeters = 150,
                outOfRangePolicy = "Block",
            });
        Assert.Equal(HttpStatusCode.OK, loc.StatusCode);

        var shiftId = await helper.WithTenantAsync(a.TenantId, async db =>
        {
            var shift = await db.Shifts.AsNoTracking().FirstAsync();
            return shift.Id;
        });
        var setShift = await clientA.PutAsJsonAsync(
            "/api/v1/attendance/settings/default-shift",
            new { shiftId });
        Assert.Equal(HttpStatusCode.OK, setShift.StatusCode);

        // B cannot see A's holidays count via isolation on punch path
        var todayB = await clientB.GetAsync("/api/v1/attendance/me/today");
        Assert.Equal(HttpStatusCode.OK, todayB.StatusCode);

        var punch = await clientA.PostAsJsonAsync(
            "/api/v1/attendance/punch",
            new
            {
                latitude = Lat,
                longitude = Lon,
                accuracyM = 20,
                positionAgeMs = 1000,
                clientRequestId = Guid.NewGuid(),
                reason = (string?)null,
            });
        var punchBody = await punch.Content.ReadAsStringAsync();
        Assert.True(punch.IsSuccessStatusCode, punchBody);
        var punched = JsonSerializer.Deserialize<PunchResultDto>(punchBody, JsonOptions)!;
        Assert.Equal("In", punched.Kind);

        var clientRequestId = await GetLastClientRequestIdAsync(helper, a.TenantId, empA);
        var replay = await clientA.PostAsJsonAsync(
            "/api/v1/attendance/punch",
            new
            {
                latitude = Lat,
                longitude = Lon,
                accuracyM = 20,
                positionAgeMs = 1000,
                clientRequestId,
                reason = (string?)null,
            });
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        var replayed = (await replay.Content.ReadFromJsonAsync<PunchResultDto>(JsonOptions))!;
        Assert.Equal(punched.PunchId, replayed.PunchId);

        // Cross-tenant: B month empty for own employee, cannot see A's punches via settings holidays differ by seed only
        var holidaysA = await clientA.GetFromJsonAsync<List<HolidayDto>>(
            "/api/v1/attendance/settings/holidays",
            JsonOptions);
        var holidaysB = await clientB.GetFromJsonAsync<List<HolidayDto>>(
            "/api/v1/attendance/settings/holidays",
            JsonOptions);
        Assert.NotNull(holidaysA);
        Assert.NotNull(holidaysB);
        Assert.NotEmpty(holidaysA);

        var createHolidayA = await clientA.PostAsJsonAsync(
            "/api/v1/attendance/settings/holidays",
            new
            {
                startsOn = "2026-12-01",
                endsOn = "2026-12-01",
                nameAr = "عطلة خاصة أ",
                nameEn = "Special A",
                isOfficial = false,
                repeatsYearly = false,
                isActive = true,
            });
        Assert.Equal(HttpStatusCode.OK, createHolidayA.StatusCode);
        var holidaysBAfter = await clientB.GetFromJsonAsync<List<HolidayDto>>(
            "/api/v1/attendance/settings/holidays",
            JsonOptions);
        Assert.DoesNotContain(holidaysBAfter!, h => h.NameEn == "Special A");

        var month = await clientA.GetFromJsonAsync<AttendanceMonthDto>(
            $"/api/v1/attendance/me/month?year={DateTime.UtcNow.Year}&month={DateTime.UtcNow.Month}",
            JsonOptions);
        Assert.NotNull(month);
        Assert.NotEmpty(month.Days);

        var rules = await clientA.GetFromJsonAsync<AttendanceRulesDto>(
            "/api/v1/attendance/settings/rules",
            JsonOptions);
        Assert.NotNull(rules);
        Assert.Equal(15, rules.GraceLateMinutes);

        var weekend = await clientA.PutAsJsonAsync(
            "/api/v1/attendance/settings/weekend",
            new { weekendDays = new[] { "fri", "sat" } });
        Assert.Equal(HttpStatusCode.OK, weekend.StatusCode);

        // Outside range blocked
        var outside = await clientA.PostAsJsonAsync(
            "/api/v1/attendance/punch",
            new
            {
                latitude = Lat + 0.05,
                longitude = Lon,
                accuracyM = 20,
                positionAgeMs = 500,
                clientRequestId = Guid.NewGuid(),
                reason = (string?)null,
            });
        Assert.Equal(HttpStatusCode.BadRequest, outside.StatusCode);
        Assert.Equal(ErrorCodes.AttendanceOutsideRange, await AuthTestHelper.ReadErrorCodeAsync(outside));
    }

    [Fact]
    public async Task Punch_requires_linked_employee()
    {
        await factory.ResetDatabaseAsync();
        var helper = new AuthTestHelper(factory);
        var a = await helper.RegisterVerifiedAsync("Att Link Co", "att-nolink@example.com");
        var session = await helper.LoginSessionAsync(a.Email, AuthTestHelper.Password, a.Slug);
        var client = helper.Authed(session.AccessToken);

        var response = await client.PostAsJsonAsync(
            "/api/v1/attendance/punch",
            new
            {
                latitude = Lat,
                longitude = Lon,
                accuracyM = 10,
                positionAgeMs = 100,
                clientRequestId = Guid.NewGuid(),
            });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(ErrorCodes.AttendanceNoEmployee, await AuthTestHelper.ReadErrorCodeAsync(response));
    }

    private static async Task<Guid> CreateLinkedEmployeeAsync(
        AuthTestHelper helper,
        HttpClient client,
        Guid tenantId,
        string ownerEmail,
        string nameAr,
        string nameEn)
    {
        var create = await client.PostAsJsonAsync(
            "/api/v1/employees",
            new
            {
                fullNameAr = nameAr,
                fullNameEn = nameEn,
                hireDate = "2024-01-15",
                status = "Active",
            });
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var emp = (await create.Content.ReadFromJsonAsync<EmployeeDetailDto>(JsonOptions))!;

        await helper.WithTenantAsync(tenantId, async db =>
        {
            var user = await db.Users.SingleAsync(u => u.Email == ownerEmail.ToLowerInvariant());
            user.SetEmployeeId(emp.Id);
            var employee = await db.Employees.SingleAsync(e => e.Id == emp.Id);
            employee.LinkUser(user.Id);
            await db.SaveChangesAsync();
            return 0;
        });

        return emp.Id;
    }

    private static Task<Guid> GetLastClientRequestIdAsync(AuthTestHelper helper, Guid tenantId, Guid employeeId) =>
        helper.WithTenantAsync(tenantId, db =>
            db.AttendancePunches
                .Where(p => p.EmployeeId == employeeId)
                .OrderByDescending(p => p.CreatedAt)
                .Select(p => p.ClientRequestId)
                .FirstAsync());
}
