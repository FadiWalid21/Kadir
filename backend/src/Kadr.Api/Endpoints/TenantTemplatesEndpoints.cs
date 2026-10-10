using System.Text.Json;
using Kadr.Api.Http;
using Kadr.Api.Localization;
using Kadr.Platform.Application.Abstractions;
using Kadr.Platform.Application.Templates;
using Kadr.SharedKernel.Constants;
using Microsoft.AspNetCore.Mvc;

namespace Kadr.Api.Endpoints;

public static class TenantTemplatesEndpoints
{
    public static IEndpointRouteBuilder MapTenantTemplatesEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/tenant/templates")
            .RequireAuthorization(PermissionCodes.TenantTemplatesManage);

        group.MapGet(string.Empty, GetOverviewAsync).WithName("GetTenantTemplatesOverview");
        group.MapGet("/{section}", ListSectionAsync).WithName("ListTenantTemplateSection");
        group.MapPut("/{section}/{id:guid}", UpdateAsync).WithName("UpdateTenantTemplateItem");
        group.MapPost("/{section}/{id:guid}/reset", ResetAsync).WithName("ResetTenantTemplateItem");
        group.MapPatch("/{section}/{id:guid}/active", SetActiveAsync).WithName("SetTenantTemplateItemActive");
        group.MapPost("/add-missing", AddMissingAsync).WithName("AddMissingTenantTemplateItems");

        return app;
    }

    private static async Task<IResult> GetOverviewAsync(
        HttpContext httpContext,
        ITenantTemplateService templates,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken)
    {
        var result = await templates.GetOverviewAsync(cancellationToken).ConfigureAwait(false);
        return ToResult(httpContext, result, localizer);
    }

    private static async Task<IResult> ListSectionAsync(
        string section,
        HttpContext httpContext,
        ITenantTemplateService templates,
        IErrorLocalizer localizer,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? q = null,
        [FromQuery] bool? active = null,
        CancellationToken cancellationToken = default)
    {
        var result = await templates.ListSectionAsync(section, page, pageSize, q, active, cancellationToken)
            .ConfigureAwait(false);
        return ToResult(httpContext, result, localizer);
    }

    private static async Task<IResult> UpdateAsync(
        string section,
        Guid id,
        HttpContext httpContext,
        ITenantTemplateService templates,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = section switch
            {
                "shifts" => await templates.UpdateShiftAsync(
                    id,
                    await httpContext.Request.ReadFromJsonAsync<UpdateShiftTemplateRequest>(cancellationToken: cancellationToken)
                        .ConfigureAwait(false) ?? new UpdateShiftTemplateRequest("", "", "09:00", "17:00", 0),
                    cancellationToken).ConfigureAwait(false),
                "leave-types" => await templates.UpdateLeaveTypeAsync(
                    id,
                    await httpContext.Request.ReadFromJsonAsync<UpdateLeaveTypeTemplateRequest>(cancellationToken: cancellationToken)
                        .ConfigureAwait(false) ?? new UpdateLeaveTypeTemplateRequest("", "", false, null, true, false),
                    cancellationToken).ConfigureAwait(false),
                "document-types" => await templates.UpdateDocumentTypeAsync(
                    id,
                    await httpContext.Request.ReadFromJsonAsync<UpdateDocumentTypeTemplateRequest>(cancellationToken: cancellationToken)
                        .ConfigureAwait(false) ?? new UpdateDocumentTypeTemplateRequest("", "", false, []),
                    cancellationToken).ConfigureAwait(false),
                "job-titles" => await templates.UpdateJobTitleAsync(
                    id,
                    await httpContext.Request.ReadFromJsonAsync<UpdateNamedTemplateRequest>(cancellationToken: cancellationToken)
                        .ConfigureAwait(false) ?? new UpdateNamedTemplateRequest("", ""),
                    cancellationToken).ConfigureAwait(false),
                "departments" => await templates.UpdateDepartmentAsync(
                    id,
                    await httpContext.Request.ReadFromJsonAsync<UpdateNamedTemplateRequest>(cancellationToken: cancellationToken)
                        .ConfigureAwait(false) ?? new UpdateNamedTemplateRequest("", ""),
                    cancellationToken).ConfigureAwait(false),
                _ => SharedKernel.Results.Result.Failure<TemplateItemDto>(
                    ErrorCodes.TemplateSectionInvalid,
                    ErrorCodes.TemplateSectionInvalid),
            };
            return ToResult(httpContext, result, localizer);
        }
        catch (JsonException)
        {
            return Results.Problem(ApiProblemDetails.Create(
                httpContext,
                StatusCodes.Status400BadRequest,
                ErrorCodes.TemplateValidationFailed,
                localizer));
        }
    }

    private static async Task<IResult> ResetAsync(
        string section,
        Guid id,
        HttpContext httpContext,
        ITenantTemplateService templates,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken)
    {
        var result = await templates.ResetAsync(section, id, cancellationToken).ConfigureAwait(false);
        return ToResult(httpContext, result, localizer);
    }

    private static async Task<IResult> SetActiveAsync(
        string section,
        Guid id,
        [FromBody] SetActiveRequest request,
        HttpContext httpContext,
        ITenantTemplateService templates,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken)
    {
        var result = await templates.SetActiveAsync(section, id, request.IsActive, cancellationToken)
            .ConfigureAwait(false);
        return ToResult(httpContext, result, localizer);
    }

    private static async Task<IResult> AddMissingAsync(
        HttpContext httpContext,
        ITenantTemplateService templates,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken)
    {
        var result = await templates.AddMissingAsync(cancellationToken).ConfigureAwait(false);
        return ToResult(httpContext, result, localizer);
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

        var code = result.ErrorCode ?? ErrorCodes.TemplateValidationFailed;
        var status = code switch
        {
            ErrorCodes.TemplateItemNotFound or ErrorCodes.TenantNotFound => StatusCodes.Status404NotFound,
            ErrorCodes.TemplateSectionInvalid => StatusCodes.Status400BadRequest,
            ErrorCodes.TemplateSourceMissing => StatusCodes.Status400BadRequest,
            ErrorCodes.TemplateChangeNotAllowed => StatusCodes.Status400BadRequest,
            _ => StatusCodes.Status400BadRequest,
        };
        return Results.Problem(ApiProblemDetails.Create(httpContext, status, code, localizer));
    }
}
