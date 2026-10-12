using Kadr.Api.Http;
using Kadr.Api.Localization;
using Kadr.Platform.Application.Abstractions;
using Kadr.Platform.Application.Payroll;
using Kadr.SharedKernel.Constants;
using Microsoft.AspNetCore.Mvc;

namespace Kadr.Api.Endpoints;

public static class SalaryImportEndpoints
{
    public static IEndpointRouteBuilder MapSalaryImportEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/payroll/salaries/import").WithTags("SalaryImport");

        group.MapGet("/template", GetTemplateAsync)
            .RequireAuthorization(PermissionCodes.SalaryManage)
            .WithName("GetSalaryImportTemplate");

        group.MapPost("/preview", PreviewAsync)
            .RequireAuthorization(PermissionCodes.SalaryManage)
            .DisableAntiforgery()
            .WithName("PreviewSalaryImport");

        group.MapPost("/commit", CommitAsync)
            .RequireAuthorization(PermissionCodes.SalaryManage)
            .WithName("CommitSalaryImport");

        return app;
    }

    private static async Task<IResult> GetTemplateAsync(
        HttpContext httpContext,
        ISalaryImportService import,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken)
    {
        var result = await import.GetTemplateAsync(cancellationToken).ConfigureAwait(false);
        if (result.IsFailure)
        {
            return ToProblem(httpContext, result.ErrorCode, localizer);
        }

        return Results.File(result.Value!, "text/csv", "salary-import-template.csv");
    }

    private static async Task<IResult> PreviewAsync(
        HttpContext httpContext,
        ISalaryImportService import,
        IErrorLocalizer localizer,
        IFormFile? file,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return ToProblem(httpContext, ErrorCodes.SalaryImportInvalid, localizer);
        }

        await using var stream = file.OpenReadStream();
        var result = await import.PreviewAsync(stream, file.FileName, cancellationToken).ConfigureAwait(false);
        if (result.IsSuccess)
        {
            return Results.Ok(result.Value);
        }

        return ToProblem(httpContext, result.ErrorCode, localizer);
    }

    private static async Task<IResult> CommitAsync(
        [FromBody] IReadOnlyList<SalaryImportCommitRowDto> rows,
        HttpContext httpContext,
        ISalaryImportService import,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken)
    {
        var result = await import.CommitAsync(rows, cancellationToken).ConfigureAwait(false);
        if (result.IsSuccess)
        {
            return Results.Ok(result.Value);
        }

        return ToProblem(httpContext, result.ErrorCode, localizer);
    }

    private static IResult ToProblem(HttpContext httpContext, string? errorCode, IErrorLocalizer localizer)
    {
        var code = errorCode ?? ErrorCodes.SalaryImportInvalid;
        return Results.Problem(
            ApiProblemDetails.Create(httpContext, StatusCodes.Status400BadRequest, code, localizer));
    }
}
