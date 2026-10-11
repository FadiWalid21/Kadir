using FluentValidation;
using Kadr.Api.Localization;
using Kadr.Platform.Application.Abstractions;
using Kadr.Platform.Application.PlatformAudit;
using Kadr.SharedKernel.Constants;
using Microsoft.AspNetCore.Mvc;

namespace Kadr.Api.Endpoints;

public static class PlatformAuditLogsEndpoints
{
    public static IEndpointRouteBuilder MapPlatformAuditLogsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/platform/audit-logs", ListAsync)
            .RequireAuthorization(AuthorizationPolicies.PlatformAdmin)
            .WithName("PlatformListAuditLogs");

        return app;
    }

    private static async Task<IResult> ListAsync(
        [FromQuery] Guid? tenantId,
        [FromQuery] Guid? userId,
        [FromQuery] string? entityType,
        [FromQuery] string? action,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] int? pageSize,
        [FromQuery] string? cursor,
        HttpContext httpContext,
        IValidator<PlatformAuditLogQuery> validator,
        IPlatformAuditLogQueries audit,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken)
    {
        var query = new PlatformAuditLogQuery(tenantId, userId, entityType, action, from, to, pageSize ?? 0, cursor);
        if (await PlatformEndpointResults.ValidateAsync(query, validator, httpContext, localizer, cancellationToken)
                .ConfigureAwait(false) is { } invalid)
        {
            return invalid;
        }

        var result = await audit.ListAsync(query, cancellationToken).ConfigureAwait(false);
        return PlatformEndpointResults.ToResult(httpContext, result, localizer);
    }
}
