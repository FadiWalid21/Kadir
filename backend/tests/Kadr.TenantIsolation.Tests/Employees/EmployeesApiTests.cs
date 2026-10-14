using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kadr.Platform.Application.Employees;
using Kadr.TenantIsolation.Tests.Auth;
using Kadr.TenantIsolation.Tests.Register;

namespace Kadr.TenantIsolation.Tests.Employees;

[Collection(RegisterCollection.Name)]
public sealed class EmployeesApiTests(RegisterApiFactory factory)
{
    private static readonly JsonSerializerOptions JsonOptions = AuthTestHelper.JsonOptions;

    [Fact]
    public async Task Owner_can_create_and_list_employees_isolated_by_tenant()
    {
        await factory.ResetDatabaseAsync();
        var helper = new AuthTestHelper(factory);
        var a = await helper.RegisterVerifiedAsync("Emp Co A", "emp-owner-a@example.com");
        var b = await helper.RegisterVerifiedAsync("Emp Co B", "emp-owner-b@example.com");

        var sessionA = await helper.LoginSessionAsync(a.Email, AuthTestHelper.Password, a.Slug);
        var sessionB = await helper.LoginSessionAsync(b.Email, AuthTestHelper.Password, b.Slug);
        var clientA = helper.Authed(sessionA.AccessToken);
        var clientB = helper.Authed(sessionB.AccessToken);

        var create = await clientA.PostAsJsonAsync(
            "/api/v1/employees",
            new
            {
                fullNameAr = "أحمد علي",
                fullNameEn = "Ahmed Ali",
                hireDate = "2024-01-15",
                status = "Active",
            });
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var created = (await create.Content.ReadFromJsonAsync<EmployeeDetailDto>(JsonOptions))!;
        Assert.Equal("أحمد علي", created.FullNameAr);
        Assert.StartsWith("EMP-", created.EmployeeNumber);

        var listResponse = await clientA.GetAsync("/api/v1/employees?page=1&pageSize=20");
        var listBody = await listResponse.Content.ReadAsStringAsync();
        if (!listResponse.IsSuccessStatusCode)
        {
            throw new Xunit.Sdk.XunitException($"List failed {(int)listResponse.StatusCode}: {listBody}");
        }

        var listA = System.Text.Json.JsonSerializer.Deserialize<PagedEmployeesDto>(listBody, JsonOptions);
        Assert.NotNull(listA);
        Assert.Equal(1, listA.TotalCount);
        Assert.Equal(1, listA.Summary.ActiveForCapacity);

        var listB = await clientB.GetFromJsonAsync<PagedEmployeesDto>("/api/v1/employees", JsonOptions);
        Assert.NotNull(listB);
        Assert.Equal(0, listB.TotalCount);

        var cross = await clientB.GetAsync($"/api/v1/employees/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, cross.StatusCode);

        var org = await clientA.GetFromJsonAsync<List<OrgDepartmentNodeDto>>("/api/v1/org/tree", JsonOptions);
        Assert.NotNull(org);
        Assert.NotEmpty(org);

        var lookups = await clientA.GetFromJsonAsync<OrgLookupsDto>("/api/v1/org/lookups", JsonOptions);
        Assert.NotNull(lookups);
        Assert.NotEmpty(lookups.Branches);

        var status = await clientA.PostAsJsonAsync(
            $"/api/v1/employees/{created.Id}/status",
            new { status = "OnLeave" });
        Assert.Equal(HttpStatusCode.OK, status.StatusCode);

        var terminate = await clientA.PostAsJsonAsync(
            $"/api/v1/employees/{created.Id}/status",
            new { status = "Terminated" });
        Assert.Equal(HttpStatusCode.OK, terminate.StatusCode);
        var terminated = await clientA.GetFromJsonAsync<EmployeeDetailDto>(
            $"/api/v1/employees/{created.Id}",
            JsonOptions);
        Assert.NotNull(terminated);
        Assert.Equal("Terminated", terminated.Status);

        var template = await clientA.GetAsync("/api/v1/employees/import/template");
        Assert.Equal(HttpStatusCode.OK, template.StatusCode);
        Assert.True((await template.Content.ReadAsByteArrayAsync()).Length > 0);

        var csv =
            "nameAr,nameEn,nationalId,email,phone,hireDate,status,department,jobTitle,branch,employeeNumber\n" +
            "سارة محمد,Sara Mohamed,,sara@example.com,,2024-02-01,Active,,,,\n";
        using var previewContent = new MultipartFormDataContent();
        previewContent.Add(new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes(csv)), "file", "import.csv");
        var preview = await clientA.PostAsync("/api/v1/employees/import/preview", previewContent);
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        var previewDto = (await preview.Content.ReadFromJsonAsync<ImportPreviewResultDto>(JsonOptions))!;
        Assert.True(previewDto.ValidCount >= 1, $"expected valid rows, errors={previewDto.ErrorCount}");

        var commit = await clientA.PostAsJsonAsync("/api/v1/employees/import/commit", previewDto.ValidRows);
        Assert.Equal(HttpStatusCode.OK, commit.StatusCode);

        var expiring = await clientA.GetAsync("/api/v1/documents/expiring?window=all&page=1&pageSize=20");
        Assert.Equal(HttpStatusCode.OK, expiring.StatusCode);
    }

    [Fact]
    public async Task Employee_list_is_forbidden_without_permission()
    {
        await factory.ResetDatabaseAsync();
        var helper = new AuthTestHelper(factory);
        var account = await helper.RegisterVerifiedAsync("Emp Co", "emp-owner@example.com");
        var session = await helper.LoginSessionAsync(account.Email, AuthTestHelper.Password, account.Slug);
        // Anonymous
        var anon = factory.CreateClient();
        var response = await anon.GetAsync("/api/v1/employees");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        // Authenticated owner still has employees.read after role sync
        var client = helper.Authed(session.AccessToken);
        var ok = await client.GetAsync("/api/v1/employees?page=1&pageSize=10");
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
    }
}
