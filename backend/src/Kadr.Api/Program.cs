using Kadr.Api.Middleware;
using Kadr.Identity.Application;
using Kadr.Identity.Infrastructure;
using Kadr.Infrastructure.Persistence;
using Kadr.Platform.Application;
using Kadr.Platform.Infrastructure;
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
    builder.Services.AddOpenApi();
    builder.Services.AddHealthChecks();

    var app = builder.Build();

    app.UseSerilogRequestLogging();
    app.UseMiddleware<LogContextMiddleware>();

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
    }

    app.MapHealthChecks("/health");
    app.MapGet("/api/v1/health", () => Results.Ok(new { status = "ok" }))
        .WithName("ApiHealth");

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
