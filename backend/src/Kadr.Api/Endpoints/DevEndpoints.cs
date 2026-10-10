using Kadr.Infrastructure.Persistence;
using Kadr.Platform.Application.Abstractions;
using Kadr.SharedKernel.Constants;
using Kadr.SharedKernel.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Kadr.Api.Endpoints;

public static class DevEndpoints
{
    public static IEndpointRouteBuilder MapDevEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/dev/permission-check", () => Results.Ok(new { ok = true }))
            .RequireAuthorization(PermissionCodes.UsersInvite)
            .WithName("DevPermissionCheck");

        app.MapPost("/api/v1/dev/templates/backfill", BackfillTemplatesAsync)
            .RequireAuthorization(PermissionCodes.TenantTemplatesManage)
            .WithName("DevBackfillTemplates");

        return app;
    }

    private static async Task<IResult> BackfillTemplatesAsync(
        ITenantContext tenantContext,
        KadrDbContext db,
        ITemplateApplyService apply,
        CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is null)
        {
            return Results.Unauthorized();
        }

        var tenant = await db.Tenants.AsNoTracking()
            .SingleOrDefaultAsync(t => t.Id == tenantContext.TenantId.Value, cancellationToken)
            .ConfigureAwait(false);
        if (tenant is null)
        {
            return Results.NotFound();
        }

        var added = await apply.AddMissingAsync(tenant.Id, tenant.IndustryCode, cancellationToken)
            .ConfigureAwait(false);
        return Results.Ok(new { added });
    }
}
