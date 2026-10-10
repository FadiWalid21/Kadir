using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Kadr.Api.Endpoints;
using Kadr.Identity.Application.Auth;
using Kadr.Identity.Domain.Roles;
using Kadr.Infrastructure.Persistence;
using Kadr.SharedKernel.Constants;
using Kadr.SharedKernel.Tenancy;
using Kadr.TenantIsolation.Tests.Register;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SkiaSharp;
using Xunit;

namespace Kadr.TenantIsolation.Tests.Branding;

[Collection(RegisterCollection.Name)]
public sealed class TenantBrandingTests(RegisterApiFactory factory)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private const string Password = "Str0ngPass!!";

    [Fact]
    public async Task Owner_can_rename_upload_replace_delete_logo_and_me_exposes_logoUrl()
    {
        await factory.ResetDatabaseAsync();
        var client = factory.CreateCookieClient();
        var (slug, email, _) = await RegisterVerifiedAsync(client, "Brand Co", "brand-owner@example.com");
        var token = await LoginAccessTokenAsync(client, email, slug);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var renamed = await client.PutAsJsonAsync(
            "/api/v1/tenant/settings",
            new UpdateTenantSettingsRequest("شركة العلامة الطويلة جداً للاختبار", "ar", "Africa/Cairo"));
        Assert.Equal(HttpStatusCode.OK, renamed.StatusCode);
        var settings = await renamed.Content.ReadFromJsonAsync<TenantSettingsResponse>(JsonOptions);
        Assert.Equal("شركة العلامة الطويلة جداً للاختبار", settings!.Name);

        var upload = await UploadLogoAsync(client, CreatePng(80, 80));
        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
        var logo = await upload.Content.ReadFromJsonAsync<TenantLogoResponse>(JsonOptions);
        Assert.NotNull(logo);
        Assert.StartsWith("/api/v1/files/", logo.LogoUrl, StringComparison.Ordinal);

        var me = await client.GetAsync("/api/v1/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        var meBody = await me.Content.ReadFromJsonAsync<MeResponse>(JsonOptions);
        Assert.Equal(logo.LogoUrl, meBody!.Tenant.LogoUrl);
        Assert.Equal("شركة العلامة الطويلة جداً للاختبار", meBody.Tenant.Name);

        var file = await client.GetAsync(logo.LogoUrl);
        Assert.Equal(HttpStatusCode.OK, file.StatusCode);
        Assert.Equal("image/png", file.Content.Headers.ContentType?.MediaType);
        var fileBytes = await file.Content.ReadAsByteArrayAsync();
        Assert.True(fileBytes.Length > 0, "Authenticated file GET must return a non-empty body.");

        var replace = await UploadLogoAsync(client, CreatePng(40, 40));
        Assert.Equal(HttpStatusCode.OK, replace.StatusCode);
        var logo2 = await replace.Content.ReadFromJsonAsync<TenantLogoResponse>(JsonOptions);
        Assert.NotEqual(logo.LogoFileId, logo2!.LogoFileId);

        var deleted = await client.DeleteAsync("/api/v1/tenant/logo");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        var meAfter = await client.GetAsync("/api/v1/me");
        var meAfterBody = await meAfter.Content.ReadFromJsonAsync<MeResponse>(JsonOptions);
        Assert.Null(meAfterBody!.Tenant.LogoUrl);
    }

    [Fact]
    public async Task Rejects_invalid_type_and_oversized_logo()
    {
        await factory.ResetDatabaseAsync();
        var client = factory.CreateCookieClient();
        var (slug, email, _) = await RegisterVerifiedAsync(client, "Bad Logo Co", "bad-logo@example.com");
        var token = await LoginAccessTokenAsync(client, email, slug);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var bad = await UploadLogoAsync(client, Encoding.UTF8.GetBytes("not-image"), "x.bin", "application/octet-stream");
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        Assert.Equal(ErrorCodes.FileInvalidType, await ReadErrorCodeAsync(bad));

        var huge = new byte[(2 * 1024 * 1024) + 8];
        CreatePng(8, 8).AsSpan(0, 8).CopyTo(huge);
        var tooBig = await UploadLogoAsync(client, huge);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, tooBig.StatusCode);
        Assert.Equal(ErrorCodes.FileTooLarge, await ReadErrorCodeAsync(tooBig));
    }

    [Fact]
    public async Task Employee_without_settings_update_gets_forbidden_and_cross_tenant_file_isolated()
    {
        await factory.ResetDatabaseAsync();
        var setup = factory.CreateCookieClient();
        var a = await RegisterVerifiedAsync(setup, "Tenant A Brand", "a-brand@example.com", "a");
        var b = await RegisterVerifiedAsync(setup, "Tenant B Brand", "b-brand@example.com", "b");

        var ownerA = factory.CreateCookieClient();
        var tokenA = await LoginAccessTokenAsync(ownerA, a.Email, a.Slug);
        ownerA.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenA);
        var uploadA = await UploadLogoAsync(ownerA, CreatePng(32, 32));
        Assert.Equal(HttpStatusCode.OK, uploadA.StatusCode);
        var logoA = await uploadA.Content.ReadFromJsonAsync<TenantLogoResponse>(JsonOptions);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KadrDbContext>();
            var ambient = scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();
            ambient.Set(a.TenantId);
            var user = await db.Users.SingleAsync(u => u.Email == a.Email);
            var employeeRole = await db.Roles.SingleAsync(r =>
                r.TenantId == a.TenantId && r.Name == SystemRoleNames.Employee);
            var ownerLink = await db.UserRoles.SingleAsync(ur => ur.UserId == user.Id);
            db.UserRoles.Remove(ownerLink);
            db.UserRoles.Add(UserRole.Create(a.TenantId, user.Id, employeeRole.Id));
            await db.SaveChangesAsync();
            scope.ServiceProvider.GetRequiredService<IPermissionCacheInvalidator>()
                .Invalidate(a.TenantId, user.Id);
        }

        var empLogin = await LoginAccessTokenAsync(factory.CreateCookieClient(), a.Email, a.Slug);
        var emp = factory.CreateCookieClient();
        emp.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", empLogin);
        var forbidden = await emp.PutAsJsonAsync(
            "/api/v1/tenant/settings",
            new UpdateTenantSettingsRequest("Nope", null, null));
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        var ownerB = factory.CreateCookieClient();
        var tokenB = await LoginAccessTokenAsync(ownerB, b.Email, b.Slug);
        ownerB.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenB);
        var cross = await ownerB.GetAsync(logoA!.LogoUrl);
        Assert.Equal(HttpStatusCode.NotFound, cross.StatusCode);

        var anon = factory.CreateCookieClient();
        var unauthenticated = await anon.GetAsync(logoA.LogoUrl);
        Assert.Equal(HttpStatusCode.Unauthorized, unauthenticated.StatusCode);
    }

    private async Task<(string Slug, string Email, Guid TenantId)> RegisterVerifiedAsync(
        HttpClient client,
        string companyName,
        string email,
        string companySuffix = "")
    {
        factory.Emails.Clear();
        var response = await RegisterAsync(client, companyName + companySuffix, email);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var reg = await response.Content.ReadFromJsonAsync<RegisterTenantResponse>(JsonOptions);
        Assert.NotNull(reg);
        var token = factory.Emails.ExtractLastVerificationToken();
        Assert.NotNull(token);
        var verify = await client.PostAsJsonAsync("/api/v1/auth/verify-email", new VerifyEmailRequest(token));
        Assert.Equal(HttpStatusCode.NoContent, verify.StatusCode);
        return (reg.Slug, email, reg.TenantId);
    }

    private static async Task<HttpResponseMessage> RegisterAsync(HttpClient client, string companyName, string email)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/register")
        {
            Content = JsonContent.Create(new RegisterTenantRequest(
                "Owner",
                email,
                Password,
                companyName,
                "office",
                "Africa/Cairo",
                "ar",
                "EG")),
        };
        request.Headers.TryAddWithoutValidation(HttpHeaderNames.IdempotencyKey, Guid.NewGuid().ToString("N"));
        return await client.SendAsync(request);
    }

    private static async Task<string> LoginAccessTokenAsync(HttpClient client, string email, string slug)
    {
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, Password, slug));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var body = await login.Content.ReadFromJsonAsync<AccessTokenResponse>(JsonOptions);
        return body!.AccessToken;
    }

    private static async Task<HttpResponseMessage> UploadLogoAsync(
        HttpClient client,
        byte[] bytes,
        string fileName = "logo.png",
        string contentType = "image/png")
    {
        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        content.Add(fileContent, "file", fileName);
        return await client.PutAsync("/api/v1/tenant/logo", content);
    }

    private static byte[] CreatePng(int width, int height)
    {
        using var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Blue);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static async Task<string?> ReadErrorCodeAsync(HttpResponseMessage response)
    {
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.TryGetProperty("code", out var code) ? code.GetString()
            : doc.RootElement.TryGetProperty("extensions", out var ext) && ext.TryGetProperty("code", out var nested)
                ? nested.GetString()
                : null;
    }
}
