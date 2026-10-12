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
public sealed class WorkLocationApiTests(RegisterApiFactory factory)
{
    private static readonly JsonSerializerOptions JsonOptions = AuthTestHelper.JsonOptions;

    // Work site near Cairo; the legacy branch is never configured in these tests.
    private const double SiteLat = 30.0444;
    private const double SiteLon = 31.2357;

    [Fact]
    public async Task Punch_inside_assigned_work_location_is_accepted_and_records_the_location()
    {
        await factory.ResetDatabaseAsync();
        var (helper, tenantId, client, employeeId) = await SetupAsync("WL Inside Co", "wl-inside@example.com");

        var locationId = await CreateLocationAsync(client, "موقع داخل", "Inside site");
        await AssignEmployeeAsync(client, locationId, employeeId);

        var punch = await PunchAsync(client, SiteLat + 0.0003, SiteLon, reason: null);
        var body = await punch.Content.ReadAsStringAsync();
        Assert.True(punch.IsSuccessStatusCode, body);
        var result = JsonSerializer.Deserialize<PunchResultDto>(body, JsonOptions)!;
        Assert.True(result.IsInside);
        Assert.Equal(locationId, result.WorkLocationId);

        var stored = await helper.WithTenantAsync(tenantId, db =>
            db.AttendancePunches.AsNoTracking().Where(p => p.EmployeeId == employeeId)
                .Select(p => p.WorkLocationId).SingleAsync());
        Assert.Equal(locationId, stored);
    }

    [Fact]
    public async Task Punch_outside_assigned_work_location_is_blocked()
    {
        await factory.ResetDatabaseAsync();
        var (_, _, client, employeeId) = await SetupAsync("WL Outside Co", "wl-outside@example.com");

        var locationId = await CreateLocationAsync(client, "موقع خارج", "Outside site");
        await AssignEmployeeAsync(client, locationId, employeeId);

        var punch = await PunchAsync(client, SiteLat + 0.05, SiteLon, reason: "traffic");
        Assert.Equal(HttpStatusCode.BadRequest, punch.StatusCode);
        Assert.Equal(ErrorCodes.AttendanceOutsideRange, await AuthTestHelper.ReadErrorCodeAsync(punch));
    }

    [Fact]
    public async Task Anywhere_policy_requires_reason_then_allows_punch_outside()
    {
        await factory.ResetDatabaseAsync();
        var (_, _, client, employeeId) = await SetupAsync("WL Anywhere Co", "wl-anywhere@example.com");

        var locationId = await CreateLocationAsync(client, "موقع", "Site");
        await AssignEmployeeAsync(client, locationId, employeeId);

        var policy = await client.PutAsJsonAsync(
            $"/api/v1/attendance/employees/{employeeId}/attendance-policy",
            new { mayClockInFromAnywhere = true, anywhereRequiresReason = true, anywhereRequiresPhoto = false });
        Assert.Equal(HttpStatusCode.OK, policy.StatusCode);

        var missing = await PunchAsync(client, SiteLat + 0.05, SiteLon, reason: null);
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        Assert.Equal(
            ErrorCodes.AttendanceAnywhereEvidenceRequired,
            await AuthTestHelper.ReadErrorCodeAsync(missing));

        var allowed = await PunchAsync(client, SiteLat + 0.05, SiteLon, reason: "Client visit");
        var body = await allowed.Content.ReadAsStringAsync();
        Assert.True(allowed.IsSuccessStatusCode, body);
        var result = JsonSerializer.Deserialize<PunchResultDto>(body, JsonOptions)!;
        Assert.False(result.IsInside);
        Assert.Equal("Client visit", result.OutsideReason);
        Assert.Equal(locationId, result.WorkLocationId);
    }

    [Fact]
    public async Task Anywhere_policy_requiring_photo_rejects_missing_photo()
    {
        await factory.ResetDatabaseAsync();
        var (_, _, client, employeeId) = await SetupAsync("WL Photo Co", "wl-photo@example.com");

        var locationId = await CreateLocationAsync(client, "موقع", "Site");
        await AssignEmployeeAsync(client, locationId, employeeId);
        var policy = await client.PutAsJsonAsync(
            $"/api/v1/attendance/employees/{employeeId}/attendance-policy",
            new { mayClockInFromAnywhere = true, anywhereRequiresReason = false, anywhereRequiresPhoto = true });
        Assert.Equal(HttpStatusCode.OK, policy.StatusCode);

        var punch = await PunchAsync(client, SiteLat + 0.05, SiteLon, reason: "no photo");
        Assert.Equal(HttpStatusCode.BadRequest, punch.StatusCode);
        Assert.Equal(
            ErrorCodes.AttendanceAnywhereEvidenceRequired,
            await AuthTestHelper.ReadErrorCodeAsync(punch));
    }

    [Fact]
    public async Task Work_locations_are_tenant_isolated_and_paginated()
    {
        await factory.ResetDatabaseAsync();
        var helper = new AuthTestHelper(factory);
        var a = await helper.RegisterVerifiedAsync("WL Iso A", "wl-iso-a@example.com");
        var b = await helper.RegisterVerifiedAsync("WL Iso B", "wl-iso-b@example.com");
        var clientA = helper.Authed((await helper.LoginSessionAsync(a.Email, AuthTestHelper.Password, a.Slug)).AccessToken);
        var clientB = helper.Authed((await helper.LoginSessionAsync(b.Email, AuthTestHelper.Password, b.Slug)).AccessToken);

        var idA = await CreateLocationAsync(clientA, "موقع أ", "Site A only");
        await CreateLocationAsync(clientB, "موقع ب", "Site B only");

        var listA = await clientA.GetFromJsonAsync<PagedWorkLocationsDto>(
            "/api/v1/attendance/work-locations?page=1&pageSize=500", JsonOptions);
        var listB = await clientB.GetFromJsonAsync<PagedWorkLocationsDto>(
            "/api/v1/attendance/work-locations?q=site", JsonOptions);
        Assert.NotNull(listA);
        Assert.NotNull(listB);
        Assert.Equal(100, listA.PageSize);
        Assert.Equal(1, listA.TotalCount);
        Assert.Single(listB.Items);
        Assert.DoesNotContain(listB.Items, l => l.Id == idA);
        Assert.Equal("Site B only", listB.Items[0].NameEn);

        Assert.Equal(
            HttpStatusCode.NotFound,
            (await clientB.GetAsync($"/api/v1/attendance/work-locations/{idA}")).StatusCode);
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await clientB.GetAsync($"/api/v1/attendance/work-locations/{idA}/assignments")).StatusCode);
        var crossAssign = await clientB.PutAsJsonAsync(
            $"/api/v1/attendance/work-locations/{idA}/assignments",
            new { assignments = Array.Empty<object>() });
        Assert.Equal(HttpStatusCode.NotFound, crossAssign.StatusCode);
        Assert.Equal(
            ErrorCodes.AttendanceLocationNotFound,
            await AuthTestHelper.ReadErrorCodeAsync(crossAssign));
    }

    [Fact]
    public async Task Invalid_work_location_is_rejected()
    {
        await factory.ResetDatabaseAsync();
        var helper = new AuthTestHelper(factory);
        var a = await helper.RegisterVerifiedAsync("WL Invalid", "wl-invalid@example.com");
        var client = helper.Authed((await helper.LoginSessionAsync(a.Email, AuthTestHelper.Password, a.Slug)).AccessToken);

        var response = await client.PostAsJsonAsync(
            "/api/v1/attendance/work-locations",
            new
            {
                nameAr = "موقع",
                type = "ExternalSite",
                latitude = SiteLat,
                longitude = SiteLon,
                geofenceRadiusMeters = 10,
                isActive = true,
            });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(ErrorCodes.AttendanceValidationFailed, await AuthTestHelper.ReadErrorCodeAsync(response));
    }

    private async Task<(AuthTestHelper Helper, Guid TenantId, HttpClient Client, Guid EmployeeId)> SetupAsync(
        string company,
        string email)
    {
        var helper = new AuthTestHelper(factory);
        var account = await helper.RegisterVerifiedAsync(company, email);
        var session = await helper.LoginSessionAsync(account.Email, AuthTestHelper.Password, account.Slug);
        var client = helper.Authed(session.AccessToken);
        var employeeId = await CreateLinkedEmployeeAsync(helper, client, account.TenantId, account.Email);
        return (helper, account.TenantId, client, employeeId);
    }

    private static async Task<Guid> CreateLocationAsync(HttpClient client, string nameAr, string nameEn)
    {
        var response = await client.PostAsJsonAsync(
            "/api/v1/attendance/work-locations",
            new
            {
                nameAr,
                nameEn,
                type = "ExternalSite",
                latitude = SiteLat,
                longitude = SiteLon,
                geofenceRadiusMeters = 150,
                address = "Cairo",
                isActive = true,
            });
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, body);
        return JsonSerializer.Deserialize<WorkLocationDto>(body, JsonOptions)!.Id;
    }

    private static async Task AssignEmployeeAsync(HttpClient client, Guid locationId, Guid employeeId)
    {
        var response = await client.PutAsJsonAsync(
            $"/api/v1/attendance/work-locations/{locationId}/assignments",
            new { assignments = new[] { new { scope = "Employee", employeeId } } });
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, body);
    }

    private static Task<HttpResponseMessage> PunchAsync(HttpClient client, double lat, double lon, string? reason) =>
        client.PostAsJsonAsync(
            "/api/v1/attendance/punch",
            new
            {
                latitude = lat,
                longitude = lon,
                accuracyM = 20,
                positionAgeMs = 500,
                clientRequestId = Guid.NewGuid(),
                reason,
            });

    private static async Task<Guid> CreateLinkedEmployeeAsync(
        AuthTestHelper helper,
        HttpClient client,
        Guid tenantId,
        string ownerEmail)
    {
        var create = await client.PostAsJsonAsync(
            "/api/v1/employees",
            new
            {
                fullNameAr = "موظف موقع",
                fullNameEn = "Location Employee",
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
}
