using System.IdentityModel.Tokens.Jwt;
using System.Text;
using System.Threading.RateLimiting;
using Kadr.Api.Auth;
using Kadr.Api.Endpoints;
using Kadr.Api.Http;
using Kadr.Api.Localization;
using Kadr.Api.Middleware;
using Kadr.Identity.Application;
using Kadr.Identity.Application.Options;
using Kadr.Identity.Infrastructure;
using Kadr.Infrastructure.Persistence;
using Kadr.Infrastructure.Persistence.Seeding;
using Kadr.Platform.Application;
using Kadr.Platform.Application.Abstractions;
using Kadr.Platform.Infrastructure;
using Kadr.SharedKernel.Constants;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

    var connectionString = builder.Configuration.GetConnectionString("Default")
        ?? throw new InvalidOperationException("ConnectionStrings:Default is required.");

    builder.Services.AddOptions<JwtOptions>()
        .Bind(builder.Configuration.GetSection(JwtOptions.SectionName))
        .ValidateDataAnnotations()
        .ValidateOnStart();

    builder.Services.AddOptions<GoogleAuthOptions>()
        .Bind(builder.Configuration.GetSection(GoogleAuthOptions.SectionName));

    builder.Services.AddOptions<PasswordResetOptions>()
        .Bind(builder.Configuration.GetSection(PasswordResetOptions.SectionName));

    builder.Services.AddOptions<PlatformAdminOptions>()
        .Bind(builder.Configuration.GetSection(PlatformAdminOptions.SectionName));

    builder.Services.AddPlatformApplication();
    builder.Services.AddPlatformInfrastructure();
    builder.Services.AddIdentityApplication();
    builder.Services.AddIdentityInfrastructure();
    builder.Services.AddPersistence(connectionString);
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<IClientRequestInfo, HttpClientRequestInfo>();
    builder.Services.AddMemoryCache();
    builder.Services.AddSingleton<IErrorLocalizer, ErrorLocalizer>();
    builder.Services.AddScoped<IdempotencyService>();
    builder.Services.AddPermissionAuthorization();
    builder.Services.AddOpenApi();
    builder.Services.AddHealthChecks();
    builder.Services.AddProblemDetails();

    var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
        ?? new JwtOptions();

    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = jwt.Issuer,
                ValidateAudience = true,
                ValidAudience = jwt.Audience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
                    string.IsNullOrEmpty(jwt.SigningKey) ? "placeholder-will-fail-validate-on-start!!" : jwt.SigningKey)),
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(1),
                RoleClaimType = System.Security.Claims.ClaimTypes.Role,
                NameClaimType = JwtRegisteredClaimNames.Sub,
            };
            options.MapInboundClaims = false;
        });

    var registerLimit = builder.Configuration.GetValue("RateLimiting:RegisterPermitLimit", 5);
    var loginLimit = builder.Configuration.GetValue("RateLimiting:LoginPermitLimit", 10);
    var refreshLimit = builder.Configuration.GetValue("RateLimiting:RefreshPermitLimit", 30);
    var verifyLimit = builder.Configuration.GetValue("RateLimiting:VerifyPermitLimit", 10);
    var resendLimit = builder.Configuration.GetValue("RateLimiting:ResendPermitLimit", 5);
    var inviteValidateLimit = builder.Configuration.GetValue("RateLimiting:InviteValidatePermitLimit", 20);
    var providersLimit = builder.Configuration.GetValue("RateLimiting:ProvidersPermitLimit", 60);
    var googleNonceLimit = builder.Configuration.GetValue("RateLimiting:GoogleNoncePermitLimit", 20);
    var googleLoginLimit = builder.Configuration.GetValue("RateLimiting:GoogleLoginPermitLimit", 10);
    var googleSelectLimit = builder.Configuration.GetValue("RateLimiting:GoogleSelectPermitLimit", 20);
    var forgotLimit = builder.Configuration.GetValue("RateLimiting:ForgotPermitLimit", 5);
    var resetLimit = builder.Configuration.GetValue("RateLimiting:ResetPermitLimit", 10);
    var meSecurityLimit = builder.Configuration.GetValue("RateLimiting:MeSecurityPermitLimit", 10);

    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.AddPolicy("auth-register", httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = registerLimit,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                }));
        options.AddPolicy("auth-login", httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                PartitionKey(httpContext, "login"),
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = loginLimit,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                }));
        options.AddPolicy("auth-refresh", httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = refreshLimit,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                }));
        options.AddPolicy("auth-verify", httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = verifyLimit,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                }));
        options.AddPolicy("auth-resend", httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                PartitionKey(httpContext, "resend"),
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = resendLimit,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                }));
        options.AddPolicy("auth-invite-validate", httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = inviteValidateLimit,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                }));
        options.AddPolicy("auth-providers", httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = providersLimit,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                }));
        options.AddPolicy("auth-google-nonce", httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = googleNonceLimit,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                }));
        options.AddPolicy("auth-google-login", httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = googleLoginLimit,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                }));
        options.AddPolicy("auth-google-select", httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = googleSelectLimit,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                }));
        // Per-IP window here; the per-email limit is enforced inside PasswordResetService (body is not readable this early).
        options.AddPolicy("auth-forgot", httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                PartitionKey(httpContext, "forgot"),
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = forgotLimit,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                }));
        options.AddPolicy("auth-reset", httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                PartitionKey(httpContext, "reset"),
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = resetLimit,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                }));
        options.AddPolicy("me-security", httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                PartitionKey(httpContext, "me-security"),
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = meSecurityLimit,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                }));
    });

    var app = builder.Build();

    // Default Serilog request logging: method, path, status, elapsed — never request bodies or Authorization.
    app.UseSerilogRequestLogging();
    app.UseMiddleware<NoStoreCacheMiddleware>();
    app.UseRateLimiter();
    app.UseAuthentication();
    app.UseMiddleware<TenantContextMiddleware>();
    app.UseMiddleware<SubscriptionReadOnlyMiddleware>();
    app.UseMiddleware<LogContextMiddleware>();
    app.UseAuthorization();

    if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
    {
        app.MapOpenApi();
        app.MapDevEndpoints();

        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KadrDbContext>();
        await db.Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<PlanSeeder>().SeedAsync();
        await scope.ServiceProvider.GetRequiredService<IndustryTemplateSeeder>().SeedAsync();
        await scope.ServiceProvider.GetRequiredService<PlatformAdminSeeder>().SeedAsync();
    }

    app.MapHealthChecks("/health");
    app.MapGet("/api/v1/health", () => Results.Ok(new { status = "ok" }))
        .WithName("ApiHealth");
    app.MapAuthEndpoints();
    app.MapPlatformAuthEndpoints();
    app.MapPlatformTenantsEndpoints();
    app.MapPlatformPlansEndpoints();
    app.MapPlatformAuditLogsEndpoints();
    app.MapPlatformHealthEndpoints();
    app.MapGoogleAuthEndpoints();
    app.MapMeEndpoints();
    app.MapTenantEndpoints();
    app.MapTemplatesEndpoints();
    app.MapTenantTemplatesEndpoints();
    app.MapRolesEndpoints();
    app.MapUsersEndpoints();
    app.MapInvitationsEndpoints();
    app.MapSubscriptionEndpoints();
    app.MapAuditLogEndpoints();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
    throw;
}
finally
{
    Log.CloseAndFlush();
}

static string PartitionKey(HttpContext httpContext, string prefix)
{
    var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    return $"{prefix}:{ip}";
}

public partial class Program;
