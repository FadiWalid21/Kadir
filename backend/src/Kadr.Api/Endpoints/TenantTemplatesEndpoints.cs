using System.Text.Json;
using FluentValidation;
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
        group.MapPost("/{section}", CreateAsync).WithName("CreateTenantTemplateItem");
        group.MapPut("/{section}/{id:guid}", UpdateAsync).WithName("UpdateTenantTemplateItem");
        group.MapDelete("/{section}/{id:guid}", DeleteAsync).WithName("DeleteTenantTemplateItem");
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

    private static async Task<IResult> CreateAsync(
        string section,
        HttpContext httpContext,
        ITenantTemplateService templates,
        IValidator<UpdateShiftTemplateRequest> shiftValidator,
        IValidator<UpdateLeaveTypeTemplateRequest> leaveValidator,
        IValidator<UpdateDocumentTypeTemplateRequest> documentValidator,
        IValidator<UpdateNamedTemplateRequest> namedValidator,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken)
    {
        try
        {
            SharedKernel.Results.Result<TemplateItemDto> result;
            switch (section)
            {
                case "shifts":
                {
                    var request = await httpContext.Request.ReadFromJsonAsync<UpdateShiftTemplateRequest>(
                            cancellationToken: cancellationToken)
                        .ConfigureAwait(false) ?? new UpdateShiftTemplateRequest("", "", "09:00", "17:00", 0);
                    var invalid = await ValidateAsync(request, shiftValidator, httpContext, localizer, cancellationToken)
                        .ConfigureAwait(false);
                    if (invalid is not null)
                    {
                        return invalid;
                    }

                    result = await templates.CreateShiftAsync(request, cancellationToken).ConfigureAwait(false);
                    break;
                }
                case "leave-types":
                {
                    var request = await httpContext.Request.ReadFromJsonAsync<UpdateLeaveTypeTemplateRequest>(
                            cancellationToken: cancellationToken)
                        .ConfigureAwait(false) ?? new UpdateLeaveTypeTemplateRequest("", "", false, null, true, false);
                    var invalid = await ValidateAsync(request, leaveValidator, httpContext, localizer, cancellationToken)
                        .ConfigureAwait(false);
                    if (invalid is not null)
                    {
                        return invalid;
                    }

                    result = await templates.CreateLeaveTypeAsync(request, cancellationToken).ConfigureAwait(false);
                    break;
                }
                case "document-types":
                {
                    var request = await httpContext.Request.ReadFromJsonAsync<UpdateDocumentTypeTemplateRequest>(
                            cancellationToken: cancellationToken)
                        .ConfigureAwait(false) ?? new UpdateDocumentTypeTemplateRequest("", "", false, []);
                    var invalid = await ValidateAsync(
                            request, documentValidator, httpContext, localizer, cancellationToken)
                        .ConfigureAwait(false);
                    if (invalid is not null)
                    {
                        return invalid;
                    }

                    result = await templates.CreateDocumentTypeAsync(request, cancellationToken)
                        .ConfigureAwait(false);
                    break;
                }
                case "job-titles":
                {
                    var request = await httpContext.Request.ReadFromJsonAsync<UpdateNamedTemplateRequest>(
                            cancellationToken: cancellationToken)
                        .ConfigureAwait(false) ?? new UpdateNamedTemplateRequest("", "");
                    var invalid = await ValidateAsync(request, namedValidator, httpContext, localizer, cancellationToken)
                        .ConfigureAwait(false);
                    if (invalid is not null)
                    {
                        return invalid;
                    }

                    result = await templates.CreateJobTitleAsync(request, cancellationToken).ConfigureAwait(false);
                    break;
                }
                case "departments":
                {
                    var request = await httpContext.Request.ReadFromJsonAsync<UpdateNamedTemplateRequest>(
                            cancellationToken: cancellationToken)
                        .ConfigureAwait(false) ?? new UpdateNamedTemplateRequest("", "");
                    var invalid = await ValidateAsync(request, namedValidator, httpContext, localizer, cancellationToken)
                        .ConfigureAwait(false);
                    if (invalid is not null)
                    {
                        return invalid;
                    }

                    result = await templates.CreateDepartmentAsync(request, cancellationToken)
                        .ConfigureAwait(false);
                    break;
                }
                default:
                    result = SharedKernel.Results.Result.Failure<TemplateItemDto>(
                        ErrorCodes.TemplateSectionInvalid,
                        ErrorCodes.TemplateSectionInvalid);
                    break;
            }

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

    private static async Task<IResult> UpdateAsync(
        string section,
        Guid id,
        HttpContext httpContext,
        ITenantTemplateService templates,
        IValidator<UpdateShiftTemplateRequest> shiftValidator,
        IValidator<UpdateLeaveTypeTemplateRequest> leaveValidator,
        IValidator<UpdateDocumentTypeTemplateRequest> documentValidator,
        IValidator<UpdateNamedTemplateRequest> namedValidator,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken)
    {
        try
        {
            SharedKernel.Results.Result<TemplateItemDto> result;
            switch (section)
            {
                case "shifts":
                {
                    var request = await httpContext.Request.ReadFromJsonAsync<UpdateShiftTemplateRequest>(
                            cancellationToken: cancellationToken)
                        .ConfigureAwait(false) ?? new UpdateShiftTemplateRequest("", "", "09:00", "17:00", 0);
                    var invalid = await ValidateAsync(request, shiftValidator, httpContext, localizer, cancellationToken)
                        .ConfigureAwait(false);
                    if (invalid is not null)
                    {
                        return invalid;
                    }

                    result = await templates.UpdateShiftAsync(id, request, cancellationToken).ConfigureAwait(false);
                    break;
                }
                case "leave-types":
                {
                    var request = await httpContext.Request.ReadFromJsonAsync<UpdateLeaveTypeTemplateRequest>(
                            cancellationToken: cancellationToken)
                        .ConfigureAwait(false) ?? new UpdateLeaveTypeTemplateRequest("", "", false, null, true, false);
                    var invalid = await ValidateAsync(request, leaveValidator, httpContext, localizer, cancellationToken)
                        .ConfigureAwait(false);
                    if (invalid is not null)
                    {
                        return invalid;
                    }

                    result = await templates.UpdateLeaveTypeAsync(id, request, cancellationToken).ConfigureAwait(false);
                    break;
                }
                case "document-types":
                {
                    var request = await httpContext.Request.ReadFromJsonAsync<UpdateDocumentTypeTemplateRequest>(
                            cancellationToken: cancellationToken)
                        .ConfigureAwait(false) ?? new UpdateDocumentTypeTemplateRequest("", "", false, []);
                    var invalid = await ValidateAsync(
                            request, documentValidator, httpContext, localizer, cancellationToken)
                        .ConfigureAwait(false);
                    if (invalid is not null)
                    {
                        return invalid;
                    }

                    result = await templates.UpdateDocumentTypeAsync(id, request, cancellationToken)
                        .ConfigureAwait(false);
                    break;
                }
                case "job-titles":
                {
                    var request = await httpContext.Request.ReadFromJsonAsync<UpdateNamedTemplateRequest>(
                            cancellationToken: cancellationToken)
                        .ConfigureAwait(false) ?? new UpdateNamedTemplateRequest("", "");
                    var invalid = await ValidateAsync(request, namedValidator, httpContext, localizer, cancellationToken)
                        .ConfigureAwait(false);
                    if (invalid is not null)
                    {
                        return invalid;
                    }

                    result = await templates.UpdateJobTitleAsync(id, request, cancellationToken).ConfigureAwait(false);
                    break;
                }
                case "departments":
                {
                    var request = await httpContext.Request.ReadFromJsonAsync<UpdateNamedTemplateRequest>(
                            cancellationToken: cancellationToken)
                        .ConfigureAwait(false) ?? new UpdateNamedTemplateRequest("", "");
                    var invalid = await ValidateAsync(request, namedValidator, httpContext, localizer, cancellationToken)
                        .ConfigureAwait(false);
                    if (invalid is not null)
                    {
                        return invalid;
                    }

                    result = await templates.UpdateDepartmentAsync(id, request, cancellationToken)
                        .ConfigureAwait(false);
                    break;
                }
                default:
                    result = SharedKernel.Results.Result.Failure<TemplateItemDto>(
                        ErrorCodes.TemplateSectionInvalid,
                        ErrorCodes.TemplateSectionInvalid);
                    break;
            }

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

    private static async Task<IResult> DeleteAsync(
        string section,
        Guid id,
        HttpContext httpContext,
        ITenantTemplateService templates,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken)
    {
        var result = await templates.DeleteAsync(section, id, cancellationToken).ConfigureAwait(false);
        return ToResult(httpContext, result, localizer);
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

    private static async Task<IResult?> ValidateAsync<T>(
        T request,
        IValidator<T> validator,
        HttpContext httpContext,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken).ConfigureAwait(false);
        if (validation.IsValid)
        {
            return null;
        }

        var code = validation.Errors
            .Select(e => e.ErrorCode)
            .FirstOrDefault(c => c is not null && c.StartsWith("template.", StringComparison.Ordinal))
            ?? ErrorCodes.TemplateValidationFailed;
        var problem = ApiProblemDetails.Create(
            httpContext,
            StatusCodes.Status400BadRequest,
            code,
            localizer);
        problem.Extensions["errors"] = validation.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
        return Results.Problem(problem);
    }

    private static IResult ToResult(
        HttpContext httpContext,
        SharedKernel.Results.Result result,
        IErrorLocalizer localizer)
    {
        if (result.IsSuccess)
        {
            return Results.NoContent();
        }

        return ToProblem(httpContext, result.ErrorCode, localizer);
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
        var code = errorCode ?? ErrorCodes.TemplateValidationFailed;
        var status = code switch
        {
            ErrorCodes.TemplateItemNotFound or ErrorCodes.TenantNotFound => StatusCodes.Status404NotFound,
            ErrorCodes.TemplateItemInUse => StatusCodes.Status409Conflict,
            ErrorCodes.TemplateSectionInvalid => StatusCodes.Status400BadRequest,
            ErrorCodes.TemplateSourceMissing => StatusCodes.Status400BadRequest,
            ErrorCodes.TemplateChangeNotAllowed => StatusCodes.Status400BadRequest,
            ErrorCodes.TemplateDuplicateName => StatusCodes.Status400BadRequest,
            ErrorCodes.TemplateResetNotAvailable => StatusCodes.Status400BadRequest,
            _ => StatusCodes.Status400BadRequest,
        };
        return Results.Problem(ApiProblemDetails.Create(httpContext, status, code, localizer));
    }
}
