using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kadr.Platform.Application.Employees;
using Kadr.Platform.Application.Leave;
using Kadr.SharedKernel.Constants;
using Kadr.TenantIsolation.Tests.Auth;
using Kadr.TenantIsolation.Tests.Register;
using Microsoft.EntityFrameworkCore;

namespace Kadr.TenantIsolation.Tests.Leave;

[Collection(RegisterCollection.Name)]
public sealed class LeaveApiTests(RegisterApiFactory factory)
{
    private static readonly JsonSerializerOptions JsonOptions = AuthTestHelper.JsonOptions;

    [Fact]
    public async Task Create_leave_requires_attachment_when_type_requires_it_and_succeeds_with_upload()
    {
        await factory.ResetDatabaseAsync();
        var helper = new AuthTestHelper(factory);
        var a = await helper.RegisterVerifiedAsync("Leave Att Co", "leave-att@example.com");
        var session = await helper.LoginSessionAsync(a.Email, AuthTestHelper.Password, a.Slug);
        var client = helper.Authed(session.AccessToken);

        await CreateLinkedEmployeeAsync(helper, client, a.TenantId, a.Email, "موظف إجازة", "Leave Employee");

        var sickTypeId = await helper.WithTenantAsync(a.TenantId, db =>
            db.LeaveTypes.AsNoTracking()
                .Where(t => t.SourceKey == "leave.sick")
                .Select(t => t.Id)
                .SingleAsync());

        var start = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(14));
        var without = await client.PostAsJsonAsync(
            "/api/v1/leave/requests",
            new
            {
                leaveTypeId = sickTypeId,
                startDate = start.ToString("yyyy-MM-dd"),
                endDate = start.ToString("yyyy-MM-dd"),
                isHalfDay = false,
                reason = "Sick without file",
            });
        var withoutBody = await without.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.BadRequest, without.StatusCode);
        using (var doc = JsonDocument.Parse(withoutBody))
        {
            Assert.Equal(ErrorCodes.LeaveAttachmentRequired, doc.RootElement.GetProperty("code").GetString());
            var detail = doc.RootElement.GetProperty("detail").GetString();
            Assert.False(string.IsNullOrWhiteSpace(detail));
            Assert.NotEqual("An error occurred.", detail);
            Assert.NotEqual("حدث خطأ.", detail);
        }

        var png = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");
        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(png);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
        content.Add(fileContent, "file", "sick.png");
        var upload = await client.PostAsync("/api/v1/leave/attachments", content);
        var uploadBody = await upload.Content.ReadAsStringAsync();
        Assert.True(upload.IsSuccessStatusCode, uploadBody);
        var attachment = JsonSerializer.Deserialize<LeaveAttachmentDto>(uploadBody, JsonOptions)!;
        Assert.NotEqual(Guid.Empty, attachment.FileId);

        var ok = await client.PostAsJsonAsync(
            "/api/v1/leave/requests",
            new
            {
                leaveTypeId = sickTypeId,
                startDate = start.ToString("yyyy-MM-dd"),
                endDate = start.ToString("yyyy-MM-dd"),
                isHalfDay = false,
                reason = "Sick with file",
                attachmentFileId = attachment.FileId,
            });
        var okBody = await ok.Content.ReadAsStringAsync();
        Assert.True(ok.IsSuccessStatusCode, okBody);
        var created = JsonSerializer.Deserialize<LeaveRequestDto>(okBody, JsonOptions)!;
        Assert.Equal(sickTypeId, created.LeaveTypeId);
        Assert.Equal(attachment.FileId, created.AttachmentFileId);
        Assert.Equal("Pending", created.Status);
    }

    private static async Task CreateLinkedEmployeeAsync(
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
    }
}
