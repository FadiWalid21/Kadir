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

    builder.Services.AddPlatformApplication();
    builder.Services.AddPlatformInfrastructure();
    builder.Services.AddIdentityApplication();
    builder.Services.AddIdentityInfrastructure();
    builder.Services.AddPersistence(connectionString);
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
    });

    var app = builder.Build();

    // Default Serilog request logging: method, path, status, elapsed — never request bodies or Authorization.
    app.UseSerilogRequestLogging();
    app.UseRateLimiter();
    app.UseAuthentication();
    app.UseMiddleware<TenantContextMiddleware>();
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
    }

    app.MapHealthChecks("/health");
    app.MapGet("/api/v1/health", () => Results.Ok(new { status = "ok" }))
        .WithName("ApiHealth");
    app.MapAuthEndpoints();
    app.MapMeEndpoints();
    app.MapTemplatesEndpoints();

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
