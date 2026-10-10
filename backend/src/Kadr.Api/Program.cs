using System.Threading.RateLimiting;
using Kadr.Api.Endpoints;
using Kadr.Api.Http;
using Kadr.Api.Localization;
using Kadr.Api.Middleware;
using Kadr.Identity.Application;
using Kadr.Identity.Infrastructure;
using Kadr.Infrastructure.Persistence;
using Kadr.Infrastructure.Persistence.Seeding;
using Kadr.Platform.Application;
using Kadr.Platform.Infrastructure;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
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

    builder.Services.AddPlatformApplication();
    builder.Services.AddPlatformInfrastructure();
    builder.Services.AddIdentityApplication();
    builder.Services.AddIdentityInfrastructure();
    builder.Services.AddPersistence(connectionString);
    builder.Services.AddSingleton<IErrorLocalizer, ErrorLocalizer>();
    builder.Services.AddScoped<IdempotencyService>();
    builder.Services.AddOpenApi();
    builder.Services.AddHealthChecks();
    builder.Services.AddProblemDetails();
    var registerPermitLimit = builder.Configuration.GetValue("RateLimiting:RegisterPermitLimit", 5);
    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.AddPolicy("auth-register", httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = registerPermitLimit,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                }));
    });

    var app = builder.Build();

    // Default Serilog request logging: method, path, status, elapsed — never request bodies.
    app.UseSerilogRequestLogging();
    app.UseMiddleware<LogContextMiddleware>();
    app.UseRateLimiter();

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();

        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KadrDbContext>();
        await db.Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<PlanSeeder>().SeedAsync();
    }

    app.MapHealthChecks("/health");
    app.MapGet("/api/v1/health", () => Results.Ok(new { status = "ok" }))
        .WithName("ApiHealth");
    app.MapAuthEndpoints();

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

public partial class Program;
