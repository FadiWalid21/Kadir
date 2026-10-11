using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Kadr.Api.Endpoints;
using Kadr.Identity.Application.Auth;
using Kadr.SharedKernel.Constants;
using Kadr.TenantIsolation.Tests.Register;
using SkiaSharp;
using Xunit;

namespace Kadr.TenantIsolation.Tests.Http;

[Collection(RegisterCollection.Name)]
public sealed class NoStoreCacheTests(RegisterApiFactory factory)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private const string Password = "Str0ngPass!!";

    [Fact]
    public async Task Me_and_file_endpoints_send_cache_control_no_store()
    {
        await factory.ResetDatabaseAsync();
        var client = factory.CreateCookieClient();

        var register = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/register")
        {
            Content = JsonContent.Create(new RegisterTenantRequest(
                "Owner",
                "nostore@example.com",
                Password,
                "No Store Co",
                "office",
                "Africa/Cairo",
                "ar",
                "EG")),
        };
        register.Headers.TryAddWithoutValidation(HttpHeaderNames.IdempotencyKey, Guid.NewGuid().ToString("N"));
        var registered = await client.SendAsync(register);
        Assert.Equal(HttpStatusCode.Created, registered.StatusCode);
        var reg = await registered.Content.ReadFromJsonAsync<RegisterTenantResponse>(JsonOptions);
        Assert.NotNull(reg);

        var token = factory.Emails.ExtractLastVerificationToken();
        Assert.NotNull(token);
        await client.PostAsJsonAsync("/api/v1/auth/verify-email", new VerifyEmailRequest(token!));

        var login = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest("nostore@example.com", Password, reg!.Slug));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var access = await login.Content.ReadFromJsonAsync<AccessTokenResponse>(JsonOptions);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", access!.AccessToken);

        var me = await client.GetAsync("/api/v1/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        AssertNoStore(me);

        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(CreatePng(32, 32));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(fileContent, "file", "logo.png");
        var upload = await client.PutAsync("/api/v1/tenant/logo", content);
        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
        var logo = await upload.Content.ReadFromJsonAsync<TenantLogoResponse>(JsonOptions);
        Assert.NotNull(logo);

        var file = await client.GetAsync(logo!.LogoUrl);
        Assert.Equal(HttpStatusCode.OK, file.StatusCode);
        AssertNoStore(file);
    }

    private static void AssertNoStore(HttpResponseMessage response)
    {
        Assert.True(
            response.Headers.CacheControl?.NoStore == true
            || (response.Headers.TryGetValues("Cache-Control", out var values)
                && values.Any(v => v.Contains("no-store", StringComparison.OrdinalIgnoreCase))),
            "Expected Cache-Control: no-store");
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
}
