using Kadr.SharedKernel.Constants;

namespace Kadr.Api.Endpoints;

public static class DevEndpoints
{
    public static IEndpointRouteBuilder MapDevEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/dev/permission-check", () => Results.Ok(new { ok = true }))
            .RequireAuthorization(PermissionCodes.UsersInvite)
            .WithName("DevPermissionCheck");

        return app;
    }
}
