using Kadr.Identity.Application.Auth;
using Kadr.Infrastructure.Persistence;
using Kadr.Infrastructure.Persistence.Seeding;
using Kadr.SharedKernel.Abstractions;
using Kadr.SharedKernel.Time;
using Kadr.Testing.Support.Email;
using Kadr.Testing.Support.Time;
using Kadr.TenantIsolation.Tests.Auth;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;

namespace Kadr.TenantIsolation.Tests.Register;

public sealed class RegisterApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string TestGoogleClientId = "kadr-test-client.apps.googleusercontent.com";
    public const string TestSigningKey = "TEST_ONLY_signing_key_at_least_32_chars_long!!";

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("kadr_register_test")
        .WithUsername("kadr")
        .WithPassword("kadr_test_password")
        .Build();

    public CapturingEmailSender Emails { get; } = new();
    public FakeClock Clock { get; } = new();

    public async Task InitializeAsync() => await _container.StartAsync();

    public new async Task DisposeAsync()
    {
        await _container.DisposeAsync();
        await base.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Default", _container.GetConnectionString());
        builder.UseSetting("RateLimiting:RegisterPermitLimit", "1000");
        builder.UseSetting("RateLimiting:LoginPermitLimit", "1000");
        builder.UseSetting("RateLimiting:RefreshPermitLimit", "1000");
        builder.UseSetting("RateLimiting:VerifyPermitLimit", "1000");
        builder.UseSetting("RateLimiting:ResendPermitLimit", "1000");
        builder.UseSetting("RateLimiting:ProvidersPermitLimit", "1000");
        builder.UseSetting("RateLimiting:GoogleNoncePermitLimit", "1000");
        builder.UseSetting("RateLimiting:GoogleLoginPermitLimit", "1000");
        builder.UseSetting("RateLimiting:GoogleSelectPermitLimit", "1000");
        builder.UseSetting("RateLimiting:ForgotPermitLimit", "1000");
        builder.UseSetting("RateLimiting:ResetPermitLimit", "1000");
        builder.UseSetting("RateLimiting:MeSecurityPermitLimit", "1000");
        builder.UseSetting("RateLimiting:AttendancePunchPermitLimit", "1000");
        builder.UseSetting("PasswordReset:EmailPermitLimit", "1000");
        builder.UseSetting("Google:ClientId", TestGoogleClientId);
        builder.UseSetting("Jwt:SigningKey", TestSigningKey);
        builder.UseSetting("Jwt:Issuer", "kadr-test");
        builder.UseSetting("Jwt:Audience", "kadr-test-api");
        builder.UseSetting("PlatformAdmin:Email", "operator@kadr.test");
        builder.UseSetting("PlatformAdmin:Password", "PlatformOwner!!99");
        builder.UseSetting("PlatformAdmin:FullName", "Test Operator");
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>(Emails);
            // Replaceable per test class via WithWebHostBuilder; the default accepts the unsigned test credentials.
            services.RemoveAll<IGoogleIdTokenValidator>();
            services.AddSingleton<IGoogleIdTokenValidator, FakeGoogleIdTokenValidator>();
            services.RemoveAll<IClock>();
            services.AddSingleton<IClock>(Clock);
        });
    }

    public async Task ResetDatabaseAsync()
    {
        Emails.Clear();
        Clock.UtcNow = DateTimeOffset.UtcNow;

        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KadrDbContext>();
        await db.Database.EnsureDeletedAsync();
        await db.Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<PlanSeeder>().SeedAsync();
        await scope.ServiceProvider.GetRequiredService<IndustryTemplateSeeder>().SeedAsync();
        await scope.ServiceProvider.GetRequiredService<PlatformAdminSeeder>().SeedAsync();
    }

    public const string PlatformAdminEmail = "operator@kadr.test";
    public const string PlatformAdminPassword = "PlatformOwner!!99";

    public HttpClient CreateCookieClient() =>
        CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
}
