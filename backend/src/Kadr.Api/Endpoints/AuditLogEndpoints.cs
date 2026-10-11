using Kadr.Api.Http;
using Kadr.Api.Localization;
using Kadr.Platform.Application.Abstractions;
using Kadr.Platform.Application.Audit;
using Kadr.SharedKernel.Constants;
using Kadr.SharedKernel.Results;
using Microsoft.AspNetCore.Mvc;

namespace Kadr.Api.Endpoints;

public static class AuditLogEndpoints
{
    public static IEndpointRouteBuilder MapAuditLogEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/audit-logs", ListAsync)
            .RequireAuthorization(PermissionCodes.AuditRead)
            .WithName("ListAuditLogs");

        app.MapGet("/api/v1/audit-logs/export", ExportAsync)
            .RequireAuthorization(PermissionCodes.AuditRead)
            .WithName("ExportAuditLogs");

        return app;
    }

    private static async Task<IResult> ListAsync(
        [FromQuery] Guid? userId,
        [FromQuery] string? entityType,
        [FromQuery] string? action,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] int pageSize,
        [FromQuery] string? cursor,
        HttpContext httpContext,
        IAuditLogService auditLogs,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken)
    {
        var result = await auditLogs
            .ListAsync(new AuditLogQuery(userId, entityType, action, from, to, pageSize, cursor), cancellationToken)
            .ConfigureAwait(false);

        if (result.IsSuccess)
        {
            return Results.Ok(result.Value);
        }

        return ToProblem(httpContext, result.ErrorCode ?? ErrorCodes.TenantNotFound, localizer);
    }

    private static async Task<IResult> ExportAsync(
        [FromQuery] Guid? userId,
        [FromQuery] string? entityType,
        [FromQuery] string? action,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        HttpContext httpContext,
        IAuditLogService auditLogs,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken)
    {
        var result = await auditLogs
            .ExportCsvAsync(new AuditLogQuery(userId, entityType, action, from, to, 0, null), cancellationToken)
            .ConfigureAwait(false);

        if (result.IsFailure)
        {
            return ToProblem(httpContext, result.ErrorCode ?? ErrorCodes.TenantNotFound, localizer);
        }

        var fileName = $"audit-logs-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}.csv";
        return Results.File(result.Value!, "text/csv; charset=utf-8", fileName);
    }

    private static IResult ToProblem(HttpContext httpContext, string code, IErrorLocalizer localizer)
    {
        var status = code switch
        {
            ErrorCodes.TenantNotFound => StatusCodes.Status404NotFound,
            ErrorCodes.AuditExportTooLarge => StatusCodes.Status400BadRequest,
            ErrorCodes.AuditCursorInvalid => StatusCodes.Status400BadRequest,
            _ => StatusCodes.Status400BadRequest,
        };
        return Results.Problem(ApiProblemDetails.Create(httpContext, status, code, localizer));
    }
}
