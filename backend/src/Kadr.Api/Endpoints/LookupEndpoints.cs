using Kadr.Api.Localization;
using Kadr.Platform.Application.Abstractions;
using Kadr.Platform.Application.Lookups;
using Kadr.SharedKernel.Constants;
using Kadr.SharedKernel.Results;
using Microsoft.AspNetCore.Mvc;

namespace Kadr.Api.Endpoints;

public static class LookupEndpoints
{
    public static void MapLookupEndpoints(this WebApplication app)
    {
        app.MapGet("/api/v1/lookups/employees", SearchEmployeesAsync)
            .RequireAuthorization(PermissionCodes.EmployeesRead)
            .WithTags("Lookups")
            .WithName("LookupEmployees");

        app.MapGet("/api/v1/lookups/departments", SearchDepartmentsAsync)
            .RequireAuthorization(PermissionCodes.OrgRead)
            .WithTags("Lookups")
            .WithName("LookupDepartments");

        app.MapGet("/api/v1/lookups/branches", SearchBranchesAsync)
            .RequireAuthorization(PermissionCodes.OrgRead)
            .WithTags("Lookups")
            .WithName("LookupBranches");

        app.MapGet("/api/v1/lookups/job-titles", SearchJobTitlesAsync)
            .RequireAuthorization(PermissionCodes.OrgRead)
            .WithTags("Lookups")
            .WithName("LookupJobTitles");

        app.MapGet("/api/v1/lookups/legal-entities", SearchLegalEntitiesAsync)
            .RequireAuthorization(PermissionCodes.TenantSettingsRead)
            .WithTags("Lookups")
            .WithName("LookupLegalEntities");

        app.MapGet("/api/v1/platform/lookups/tenants", SearchTenantsAsync)
            .RequireAuthorization(AuthorizationPolicies.PlatformAdmin)
            .WithTags("PlatformLookups")
            .WithName("PlatformLookupTenants");
    }

    private static async Task<IResult> SearchEmployeesAsync(
        HttpContext httpContext,
        ILookupService lookups,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken,
        [FromQuery] string? q = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20) =>
        ToResult(
            httpContext,
            await lookups
                .SearchEmployeesAsync(q, page, pageSize, PreferArabic(httpContext), cancellationToken)
                .ConfigureAwait(false),
            localizer);

    private static async Task<IResult> SearchDepartmentsAsync(
        HttpContext httpContext,
        ILookupService lookups,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken,
        [FromQuery] string? q = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20) =>
        ToResult(
            httpContext,
            await lookups
                .SearchDepartmentsAsync(q, page, pageSize, PreferArabic(httpContext), cancellationToken)
                .ConfigureAwait(false),
            localizer);

    private static async Task<IResult> SearchBranchesAsync(
        HttpContext httpContext,
        ILookupService lookups,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken,
        [FromQuery] string? q = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20) =>
        ToResult(
            httpContext,
            await lookups
                .SearchBranchesAsync(q, page, pageSize, PreferArabic(httpContext), cancellationToken)
                .ConfigureAwait(false),
            localizer);

    private static async Task<IResult> SearchJobTitlesAsync(
        HttpContext httpContext,
        ILookupService lookups,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken,
        [FromQuery] string? q = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20) =>
        ToResult(
            httpContext,
            await lookups
                .SearchJobTitlesAsync(q, page, pageSize, PreferArabic(httpContext), cancellationToken)
                .ConfigureAwait(false),
            localizer);

    private static async Task<IResult> SearchLegalEntitiesAsync(
        HttpContext httpContext,
        ILookupService lookups,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken,
        [FromQuery] string? q = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20) =>
        ToResult(
            httpContext,
            await lookups
                .SearchLegalEntitiesAsync(q, page, pageSize, PreferArabic(httpContext), cancellationToken)
                .ConfigureAwait(false),
            localizer);

    private static async Task<IResult> SearchTenantsAsync(
        HttpContext httpContext,
        IPlatformLookupService lookups,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken,
        [FromQuery] string? q = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20) =>
        ToResult(
            httpContext,
            await lookups.SearchTenantsAsync(q, page, pageSize, cancellationToken).ConfigureAwait(false),
            localizer);

    private static bool PreferArabic(HttpContext httpContext)
    {
        var accept = httpContext.Request.Headers.AcceptLanguage.ToString();
        if (string.IsNullOrWhiteSpace(accept))
        {
            return true;
        }

        var primary = accept.Split(',').FirstOrDefault()?.Trim() ?? "ar";
        return primary.StartsWith("ar", StringComparison.OrdinalIgnoreCase);
    }

    private static IResult ToResult(
        HttpContext httpContext,
        Result<PagedLookupDto> result,
        IErrorLocalizer localizer)
    {
        if (result.IsSuccess)
        {
            return Results.Ok(result.Value);
        }

        var code = result.ErrorCode ?? ErrorCodes.AuthValidationFailed;
        var detail = localizer.GetDetail(code, httpContext.Request.Headers.AcceptLanguage);
        return Results.Problem(detail: detail, statusCode: StatusCodes.Status400BadRequest, title: code);
    }
}
