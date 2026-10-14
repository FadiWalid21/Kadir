using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Kadr.Api.Endpoints;
using Kadr.Identity.Application.Auth;
using Kadr.Identity.Domain.Roles;
using Kadr.Infrastructure.Persistence;
using Kadr.Platform.Application.Templates;
using Kadr.Platform.Domain.Templates;
using Kadr.SharedKernel.Constants;
using Kadr.SharedKernel.Tenancy;
using Kadr.TenantIsolation.Tests.Register;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Kadr.TenantIsolation.Tests.Templates;

[Collection(RegisterCollection.Name)]
public sealed class TenantTemplatesTests(RegisterApiFactory factory)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private const string Password = "Str0ngPass!!";

    [Fact]
    public async Task Register_applies_default_attendance_shift_from_template()
    {
        await factory.ResetDatabaseAsync();
        var session = await RegisterAndLoginAsync("Default Shift Co", "default-shift@example.com", "office");

        var def = await session.Client.GetFromJsonAsync<Kadr.Platform.Application.Attendance.AttendanceDefaultShiftDto>(
            "/api/v1/attendance/settings/default-shift",
            JsonOptions);
        Assert.NotNull(def);
        Assert.NotNull(def.ShiftId);

        var shifts = await session.Client.GetFromJsonAsync<PagedResultDto<TemplateItemDto>>(
            "/api/v1/tenant/templates/shifts?page=1&pageSize=50",
            JsonOptions);
        Assert.NotNull(shifts);
        Assert.Contains(shifts.Items, s => s.Id == def.ShiftId);
    }

    [Fact]
    public async Task Edit_leave_type_marks_customized_and_other_tenant_untouched()
    {
        await factory.ResetDatabaseAsync();
        var a = await RegisterAndLoginAsync("Edit A", "edit-a@example.com", "office");
        var b = await RegisterAndLoginAsync("Edit B", "edit-b@example.com", "office");

        var listA = await a.Client.GetFromJsonAsync<PagedResultDto<TemplateItemDto>>(
            "/api/v1/tenant/templates/leave-types?page=1&pageSize=50",
            JsonOptions);
        var annual = listA!.Items.Single(i => i.SourceKey == "leave.annual");
        var update = await a.Client.PutAsJsonAsync(
            $"/api/v1/tenant/templates/leave-types/{annual.Id}",
            new UpdateLeaveTypeTemplateRequest("إجازة معدّلة", "Custom annual", true, 30, true, false));
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var updated = await update.Content.ReadFromJsonAsync<TemplateItemDto>(JsonOptions);
        Assert.True(updated!.IsCustomized);
        Assert.Equal("Custom annual", updated.NameEn);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KadrDbContext>();
        var ambient = scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();
        ambient.Set(b.TenantId);
        var other = await db.LeaveTypes.AsNoTracking().SingleAsync(x => x.SourceKey == "leave.annual");
        Assert.False(other.IsCustomized);
        Assert.Equal("Annual leave", other.NameEn);
    }

    [Fact]
    public async Task Reset_restores_seed_values()
    {
        await factory.ResetDatabaseAsync();
        var session = await RegisterAndLoginAsync("Reset Co", "reset@example.com", "office");
        var list = await session.Client.GetFromJsonAsync<PagedResultDto<TemplateItemDto>>(
            "/api/v1/tenant/templates/leave-types?page=1&pageSize=50",
            JsonOptions);
        var annual = list!.Items.Single(i => i.SourceKey == "leave.annual");
        await session.Client.PutAsJsonAsync(
            $"/api/v1/tenant/templates/leave-types/{annual.Id}",
            new UpdateLeaveTypeTemplateRequest("x", "Changed", true, 99, true, false));

        var reset = await session.Client.PostAsync(
            $"/api/v1/tenant/templates/leave-types/{annual.Id}/reset",
            null);
        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);
        var body = await reset.Content.ReadFromJsonAsync<TemplateItemDto>(JsonOptions);
        Assert.False(body!.IsCustomized);
        Assert.Equal("Annual leave", body.NameEn);
        Assert.Equal(21, ReadInt(body.Fields["daysPerYear"]));
    }

    private static int ReadInt(object? value) =>
        value switch
        {
            JsonElement el => el.GetInt32(),
            int i => i,
            long l => (int)l,
            _ => Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture),
        };

    [Fact]
    public async Task Newer_seed_version_sets_update_available_without_auto_change()
    {
        await factory.ResetDatabaseAsync();
        var session = await RegisterAndLoginAsync("Bump Co", "bump@example.com", "office");
        var before = await session.Client.GetFromJsonAsync<TenantTemplatesOverviewDto>(
            "/api/v1/tenant/templates",
            JsonOptions);
        Assert.False(before!.UpdateAvailable);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KadrDbContext>();
            var v1 = await db.IndustryTemplates.AsNoTracking().SingleAsync(t => t.Code == "office" && t.Version == 1);
            var payload = JsonDocument.Parse(v1.PayloadJson);
            using var stream = new MemoryStream();
            await using (var writer = new Utf8JsonWriter(stream))
            {
                writer.WriteStartObject();
                foreach (var prop in payload.RootElement.EnumerateObject())
                {
                    if (prop.NameEquals("version"))
                    {
                        writer.WriteNumber("version", 2);
                    }
                    else if (prop.NameEquals("items"))
                    {
                        writer.WritePropertyName("items");
                        writer.WriteStartObject();
                        foreach (var section in prop.Value.EnumerateObject())
                        {
                            writer.WritePropertyName(section.Name);
                            if (section.NameEquals("jobTitles"))
                            {
                                writer.WriteStartArray();
                                foreach (var item in section.Value.EnumerateArray())
                                {
                                    item.WriteTo(writer);
                                }

                                writer.WriteStartObject();
                                writer.WriteString("key", "job.new_role");
                                writer.WriteString("nameAr", "دور جديد");
                                writer.WriteString("nameEn", "New role");
                                writer.WriteEndObject();
                                writer.WriteEndArray();
                            }
                            else
                            {
                                section.Value.WriteTo(writer);
                            }
                        }

                        writer.WriteEndObject();
                    }
                    else
                    {
                        prop.WriteTo(writer);
                    }
                }

                writer.WriteEndObject();
            }

            var json = System.Text.Encoding.UTF8.GetString(stream.ToArray());
            db.IndustryTemplates.Add(IndustryTemplate.Create(
                "office",
                2,
                v1.NameAr,
                v1.NameEn,
                json,
                v1.DescriptionAr,
                v1.DescriptionEn));
            await db.SaveChangesAsync();
        }

        var after = await session.Client.GetFromJsonAsync<TenantTemplatesOverviewDto>(
            "/api/v1/tenant/templates",
            JsonOptions);
        Assert.True(after!.UpdateAvailable);
        Assert.Equal(2, after.LatestVersion);
        Assert.Contains("job.new_role", after.MissingKeys);

        var jobs = await session.Client.GetFromJsonAsync<PagedResultDto<TemplateItemDto>>(
            "/api/v1/tenant/templates/job-titles?page=1&pageSize=100",
            JsonOptions);
        Assert.DoesNotContain(jobs!.Items, i => i.SourceKey == "job.new_role");

        var add = await session.Client.PostAsync("/api/v1/tenant/templates/add-missing", null);
        Assert.Equal(HttpStatusCode.OK, add.StatusCode);
        var addBody = await add.Content.ReadFromJsonAsync<AddMissingResultDto>(JsonOptions);
        Assert.Contains("job.new_role", addBody!.AddedKeys);

        jobs = await session.Client.GetFromJsonAsync<PagedResultDto<TemplateItemDto>>(
            "/api/v1/tenant/templates/job-titles?page=1&pageSize=100",
            JsonOptions);
        Assert.Contains(jobs!.Items, i => i.SourceKey == "job.new_role");
        Assert.Equal(5, jobs.Items.Count(i => i.SourceKey != "job.new_role"));
    }

    [Fact]
    public async Task Employee_without_permission_gets_forbidden_and_industry_change_rejected()
    {
        await factory.ResetDatabaseAsync();
        var setup = factory.CreateCookieClient();
        var reg = await RegisterVerifiedAsync(setup, "Perm Co", "perm-owner@example.com");
        var ownerToken = await LoginAccessTokenAsync(setup, reg.Email, reg.Slug);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KadrDbContext>();
            var ambient = scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();
            ambient.Set(reg.TenantId);
            var user = await db.Users.SingleAsync(u => u.Email == reg.Email);
            var employeeRole = await db.Roles.SingleAsync(r =>
                r.TenantId == reg.TenantId && r.Name == SystemRoleNames.Employee);
            var ownerLink = await db.UserRoles.SingleAsync(ur => ur.UserId == user.Id);
            db.UserRoles.Remove(ownerLink);
            db.UserRoles.Add(UserRole.Create(reg.TenantId, user.Id, employeeRole.Id));
            await db.SaveChangesAsync();
            scope.ServiceProvider.GetRequiredService<IPermissionCacheInvalidator>().Invalidate(reg.TenantId, user.Id);
        }

        var empToken = await LoginAccessTokenAsync(factory.CreateCookieClient(), reg.Email, reg.Slug);
        var emp = factory.CreateCookieClient();
        emp.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", empToken);
        var forbidden = await emp.GetAsync("/api/v1/tenant/templates");
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        var owner = factory.CreateCookieClient();
        owner.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        // re-login as owner after we stripped role — need a fresh owner; use another tenant path:
        // register second owner for industry reject on settings
        var ownerSession = await RegisterAndLoginAsync("Industry Guard", "industry-guard@example.com", "office");
        var reject = await ownerSession.Client.PutAsJsonAsync(
            "/api/v1/tenant/settings",
            new UpdateTenantSettingsRequest("Name", "ar", "Africa/Cairo", "retail"));
        Assert.Equal(HttpStatusCode.BadRequest, reject.StatusCode);
        var json = await reject.Content.ReadAsStringAsync();
        Assert.Contains(ErrorCodes.TemplateChangeNotAllowed, json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Template_rows_are_tenant_isolated()
    {
        await factory.ResetDatabaseAsync();
        var a = await RegisterAndLoginAsync("Iso A", "iso-a-tpl@example.com", "retail");
        var b = await RegisterAndLoginAsync("Iso B", "iso-b-tpl@example.com", "retail");
        var listA = await a.Client.GetFromJsonAsync<PagedResultDto<TemplateItemDto>>(
            "/api/v1/tenant/templates/shifts?page=1&pageSize=50",
            JsonOptions);
        var shiftId = listA!.Items[0].Id;
        var cross = await b.Client.GetAsync($"/api/v1/tenant/templates/shifts/{shiftId}/reset");
        // wrong method path for get — use reset POST which must 404 for other tenant
        var reset = await b.Client.PostAsync($"/api/v1/tenant/templates/shifts/{shiftId}/reset", null);
        Assert.Equal(HttpStatusCode.NotFound, reset.StatusCode);
    }

    [Fact]
    public async Task Create_custom_department_and_reject_duplicate_name()
    {
        await factory.ResetDatabaseAsync();
        var session = await RegisterAndLoginAsync("Create Co", "create-tpl@example.com", "office");
        var create = await session.Client.PostAsJsonAsync(
            "/api/v1/tenant/templates/departments",
            new UpdateNamedTemplateRequest("قسم مخصص", "Custom Dept"));
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<TemplateItemDto>(JsonOptions);
        Assert.NotNull(created);
        Assert.Null(created.SourceKey);
        Assert.True(created.IsCustomized);
        Assert.True(created.IsActive);

        var dup = await session.Client.PostAsJsonAsync(
            "/api/v1/tenant/templates/departments",
            new UpdateNamedTemplateRequest("قسم مخصص", "Other En"));
        Assert.Equal(HttpStatusCode.BadRequest, dup.StatusCode);
        var dupBody = await dup.Content.ReadAsStringAsync();
        Assert.Contains(ErrorCodes.TemplateDuplicateName, dupBody, StringComparison.Ordinal);

        var reset = await session.Client.PostAsync(
            $"/api/v1/tenant/templates/departments/{created.Id}/reset",
            null);
        Assert.Equal(HttpStatusCode.BadRequest, reset.StatusCode);
        var resetBody = await reset.Content.ReadAsStringAsync();
        Assert.Contains(ErrorCodes.TemplateResetNotAvailable, resetBody, StringComparison.Ordinal);

        var delete = await session.Client.DeleteAsync(
            $"/api/v1/tenant/templates/departments/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
    }

    [Fact]
    public async Task Delete_referenced_job_title_returns_in_use_and_create_is_tenant_isolated()
    {
        await factory.ResetDatabaseAsync();
        var a = await RegisterAndLoginAsync("Ref A", "ref-a-tpl@example.com", "office");
        var b = await RegisterAndLoginAsync("Ref B", "ref-b-tpl@example.com", "office");

        var create = await a.Client.PostAsJsonAsync(
            "/api/v1/tenant/templates/job-titles",
            new UpdateNamedTemplateRequest("مسمى مخصص", "Custom Title"));
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<TemplateItemDto>(JsonOptions);
        Assert.NotNull(created);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KadrDbContext>();
            var ambient = scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();
            ambient.Set(a.TenantId);
            var branch = await db.Branches.AsNoTracking().FirstAsync();
            var legal = await db.LegalEntities.AsNoTracking().FirstAsync();
            var hireDate = DateOnly.FromDateTime(DateTime.UtcNow.Date);
            var emp = Kadr.Platform.Domain.Employees.Employee.Hire(
                a.TenantId,
                legal.Id,
                branch.Id,
                "EMP-TPL-1",
                "موظف مرجعي",
                "Ref Employee",
                hireDate,
                Kadr.Platform.Domain.Employees.EmployeeStatus.Active,
                Kadr.Platform.Domain.Employees.ContractType.Unlimited);
            emp.UpdateJob(
                branch.Id,
                null,
                created.Id,
                null,
                hireDate,
                null,
                Kadr.Platform.Domain.Employees.ContractType.Unlimited,
                null,
                null);
            db.Employees.Add(emp);
            await db.SaveChangesAsync();
        }

        var delete = await a.Client.DeleteAsync($"/api/v1/tenant/templates/job-titles/{created.Id}");
        Assert.Equal(HttpStatusCode.Conflict, delete.StatusCode);
        var deleteBody = await delete.Content.ReadAsStringAsync();
        Assert.Contains(ErrorCodes.TemplateItemInUse, deleteBody, StringComparison.Ordinal);

        var cross = await b.Client.DeleteAsync($"/api/v1/tenant/templates/job-titles/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, cross.StatusCode);
    }

    [Fact]
    public async Task Create_without_permission_is_forbidden()
    {
        await factory.ResetDatabaseAsync();
        var setup = factory.CreateCookieClient();
        var reg = await RegisterVerifiedAsync(setup, "NoPerm Co", "noperm-tpl@example.com");

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KadrDbContext>();
            var ambient = scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();
            ambient.Set(reg.TenantId);
            var user = await db.Users.SingleAsync(u => u.Email == reg.Email);
            var employeeRole = await db.Roles.SingleAsync(r =>
                r.TenantId == reg.TenantId && r.Name == SystemRoleNames.Employee);
            var ownerLink = await db.UserRoles.SingleAsync(ur => ur.UserId == user.Id);
            db.UserRoles.Remove(ownerLink);
            db.UserRoles.Add(UserRole.Create(reg.TenantId, user.Id, employeeRole.Id));
            await db.SaveChangesAsync();
            scope.ServiceProvider.GetRequiredService<IPermissionCacheInvalidator>().Invalidate(reg.TenantId, user.Id);
        }

        var empToken = await LoginAccessTokenAsync(factory.CreateCookieClient(), reg.Email, reg.Slug);
        var emp = factory.CreateCookieClient();
        emp.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", empToken);
        var forbidden = await emp.PostAsJsonAsync(
            "/api/v1/tenant/templates/departments",
            new UpdateNamedTemplateRequest("س", "E"));
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }

    private async Task<(HttpClient Client, Guid TenantId)> RegisterAndLoginAsync(
        string company,
        string email,
        string industry)
    {
        var client = factory.CreateCookieClient();
        var (slug, _, tenantId) = await RegisterVerifiedAsync(client, company, email, industry);
        var token = await LoginAccessTokenAsync(client, email, slug);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return (client, tenantId);
    }

    private async Task<(string Slug, string Email, Guid TenantId)> RegisterVerifiedAsync(
        HttpClient client,
        string companyName,
        string email,
        string industry = "office")
    {
        factory.Emails.Clear();
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/register")
        {
            Content = JsonContent.Create(new RegisterTenantRequest(
                "Owner",
                email,
                Password,
                companyName,
                industry,
                "Africa/Cairo",
                "ar",
                "EG")),
        };
        request.Headers.TryAddWithoutValidation(HttpHeaderNames.IdempotencyKey, Guid.NewGuid().ToString("N"));
        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var reg = await response.Content.ReadFromJsonAsync<RegisterTenantResponse>(JsonOptions);
        Assert.NotNull(reg);
        var token = factory.Emails.ExtractLastVerificationToken();
        Assert.NotNull(token);
        var verify = await client.PostAsJsonAsync("/api/v1/auth/verify-email", new VerifyEmailRequest(token));
        Assert.Equal(HttpStatusCode.NoContent, verify.StatusCode);
        return (reg.Slug, email, reg.TenantId);
    }

    private static async Task<string> LoginAccessTokenAsync(HttpClient client, string email, string slug)
    {
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, Password, slug));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var body = await login.Content.ReadFromJsonAsync<AccessTokenResponse>(JsonOptions);
        return body!.AccessToken;
    }
}
