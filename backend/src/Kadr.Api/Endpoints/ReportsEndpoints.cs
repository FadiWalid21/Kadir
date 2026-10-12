using Kadr.Api.Filters;
using Kadr.Api.Http;
using Kadr.Api.Localization;
using Kadr.Platform.Application.Abstractions;
using Kadr.Platform.Application.Reports;
using Kadr.SharedKernel.Constants;
using Microsoft.AspNetCore.Mvc;

namespace Kadr.Api.Endpoints;

public static class ReportsEndpoints
{
    public static IEndpointRouteBuilder MapReportsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/reports").WithTags("Reports");

        group.MapGet(string.Empty, ListHubAsync)
            .RequireAuthorization(PermissionCodes.ReportsRead)
            .RequireFeature(FeatureKeys.Reports)
            .WithName("ReportsHub");

        group.MapGet("/headcount", GetHeadcountAsync)
            .RequireAuthorization(PermissionCodes.ReportsRead)
            .RequireFeature(FeatureKeys.Reports)
            .WithName("ReportsHeadcount");

        group.MapGet("/headcount/export", ExportHeadcountAsync)
            .RequireAuthorization(PermissionCodes.ReportsExport)
            .RequireFeature(FeatureKeys.Reports)
            .WithName("ReportsHeadcountExport");

        return app;
    }

    private static async Task<IResult> ListHubAsync(
        HttpContext httpContext,
        IHeadcountReportService reports,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken)
    {
        var result = await reports.ListHubAsync(cancellationToken).ConfigureAwait(false);
        return ToResult(httpContext, result, localizer);
    }

    private static async Task<IResult> GetHeadcountAsync(
        HttpContext httpContext,
        IHeadcountReportService reports,
        IErrorLocalizer localizer,
        [FromQuery] DateOnly? asOf,
        [FromQuery] Guid? branchId,
        [FromQuery] Guid? departmentId,
        [FromQuery] string[]? status,
        [FromQuery] string[]? contractType,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? sortBy = null,
        [FromQuery] bool sortDesc = false,
        CancellationToken cancellationToken = default)
    {
        var query = new HeadcountReportQuery(
            asOf,
            branchId,
            departmentId,
            status,
            contractType,
            page,
            pageSize,
            sortBy,
            sortDesc);
        var result = await reports.GetHeadcountAsync(query, cancellationToken).ConfigureAwait(false);
        return ToResult(httpContext, result, localizer);
    }

    private static async Task<IResult> ExportHeadcountAsync(
        HttpContext httpContext,
        IHeadcountReportService reports,
        IErrorLocalizer localizer,
        [FromQuery] string format = "xlsx",
        [FromQuery] DateOnly? asOf = null,
        [FromQuery] Guid? branchId = null,
        [FromQuery] Guid? departmentId = null,
        [FromQuery] string[]? status = null,
        [FromQuery] string[]? contractType = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] bool sortDesc = false,
        CancellationToken cancellationToken = default)
    {
        var query = new HeadcountReportQuery(
            asOf,
            branchId,
            departmentId,
            status,
            contractType,
            1,
            ReportRangePageSize.ExportPage,
            sortBy,
            sortDesc);
        var acceptLanguage = httpContext.Request.Headers.AcceptLanguage.ToString();
        var result = await reports
            .ExportHeadcountAsync(query, format, acceptLanguage, cancellationToken)
            .ConfigureAwait(false);
        if (result.IsFailure)
        {
            return ToProblem(httpContext, result.ErrorCode, localizer);
        }

        var file = result.Value!;
        return Results.File(file.Content, file.ContentType, file.FileName);
    }

    private static IResult ToResult<T>(
        HttpContext httpContext,
        SharedKernel.Results.Result<T> result,
        IErrorLocalizer localizer)
    {
        if (result.IsSuccess)
        {
            return Results.Ok(result.Value);
        }

        return ToProblem(httpContext, result.ErrorCode, localizer);
    }

    private static IResult ToProblem(HttpContext httpContext, string? errorCode, IErrorLocalizer localizer)
    {
        var code = errorCode ?? ErrorCodes.ReportsValidationFailed;
        var status = code switch
        {
            ErrorCodes.ReportsNotFound => StatusCodes.Status404NotFound,
            ErrorCodes.ReportsForbidden or ErrorCodes.PlanFeatureNotAvailable => StatusCodes.Status403Forbidden,
            ErrorCodes.TenantNotFound => StatusCodes.Status404NotFound,
            _ => StatusCodes.Status400BadRequest,
        };
        return Results.Problem(ApiProblemDetails.Create(httpContext, status, code, localizer));
    }

    private static class ReportRangePageSize
    {
        public const int ExportPage = 100;
    }
}
