using Kadr.SharedKernel.Constants;

namespace Kadr.Api.Endpoints;

public static class TemplatesEndpoints
{
    public static IEndpointRouteBuilder MapTemplatesEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/templates/industries", () =>
            {
                var items = IndustryCodes.Catalog
                    .Select(i => new IndustryDto(i.Code, i.NameAr, i.NameEn))
                    .ToArray();
                return Results.Ok(items);
            })
            .AllowAnonymous()
            .RequireRateLimiting("auth-register")
            .WithName("ListIndustries");

        return app;
    }
}

public sealed record IndustryDto(string Code, string NameAr, string NameEn);
