using Kadr.Api.Filters;
using Kadr.Api.Http;
using Kadr.Api.Localization;
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

        app.MapPost("/api/v1/dev/subscription", SetSubscriptionAsync)
            .RequireAuthorization()
            .WithName("DevSetSubscription");

        app.MapPost("/api/v1/dev/feature-check", () => Results.Ok(new { ok = true }))
            .RequireAuthorization()
            .RequireFeature(FeatureKeys.Shifts)
            .WithName("DevFeatureCheck");

        return app;
    }

    private static async Task<IResult> SetSubscriptionAsync(
        HttpContext httpContext,
        DevSubscriptionRequest body,
        IDevSubscriptionService service,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken)
    {
        var result = await service.ApplyAsync(body, cancellationToken).ConfigureAwait(false);
        if (result.IsSuccess)
        {
            return Results.NoContent();
        }

        var code = result.ErrorCode ?? ErrorCodes.SubscriptionNotFound;
        var status = code is ErrorCodes.PlanNotFound or ErrorCodes.SubscriptionNotFound
            ? StatusCodes.Status404NotFound
            : StatusCodes.Status400BadRequest;
        return Results.Problem(ApiProblemDetails.Create(httpContext, status, code, localizer));
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
