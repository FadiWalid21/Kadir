using FluentValidation;
using Kadr.Api.Http;
using Kadr.Api.Localization;
using Kadr.Platform.Application.Abstractions;
using Kadr.Platform.Application.Branches;
using Kadr.SharedKernel.Constants;
using Microsoft.AspNetCore.Mvc;

namespace Kadr.Api.Endpoints;

public static class BranchSettingsEndpoints
{
    public static IEndpointRouteBuilder MapBranchSettingsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/settings/branches").WithTags("SettingsBranches");

        group.MapGet(string.Empty, ListAsync)
            .RequireAuthorization(PermissionCodes.TenantSettingsRead)
            .WithName("ListSettingsBranches");

        group.MapPost(string.Empty, CreateAsync)
            .RequireAuthorization(PermissionCodes.TenantSettingsUpdate)
            .WithName("CreateSettingsBranch");

        group.MapPut("/{id:guid}", UpdateAsync)
            .RequireAuthorization(PermissionCodes.TenantSettingsUpdate)
            .WithName("UpdateSettingsBranch");

        group.MapPatch("/{id:guid}/active", SetActiveAsync)
            .RequireAuthorization(PermissionCodes.TenantSettingsUpdate)
            .WithName("SetSettingsBranchActive");

        group.MapDelete("/{id:guid}", DeleteAsync)
            .RequireAuthorization(PermissionCodes.TenantSettingsUpdate)
            .WithName("DeleteSettingsBranch");

        return app;
    }

    private static async Task<IResult> ListAsync(
        HttpContext httpContext,
        IBranchSettingsService branches,
        IErrorLocalizer localizer,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? q = null,
        [FromQuery] bool? active = null,
        CancellationToken cancellationToken = default)
    {
        var result = await branches.ListAsync(page, pageSize, q, active, cancellationToken)
            .ConfigureAwait(false);
        return ToResult(httpContext, result, localizer);
    }

    private static async Task<IResult> CreateAsync(
        [FromBody] CreateBranchRequest request,
        HttpContext httpContext,
        IBranchSettingsService branches,
        IValidator<CreateBranchRequest> validator,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken)
    {
        var invalid = await ValidateAsync(request, validator, httpContext, localizer, cancellationToken)
            .ConfigureAwait(false);
        if (invalid is not null)
        {
            return invalid;
        }

        var result = await branches.CreateAsync(request, cancellationToken).ConfigureAwait(false);
        return ToResult(httpContext, result, localizer);
    }

    private static async Task<IResult> UpdateAsync(
        Guid id,
        [FromBody] UpdateBranchRequest request,
        HttpContext httpContext,
        IBranchSettingsService branches,
        IValidator<UpdateBranchRequest> validator,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken)
    {
        var invalid = await ValidateAsync(request, validator, httpContext, localizer, cancellationToken)
            .ConfigureAwait(false);
        if (invalid is not null)
        {
            return invalid;
        }

        var result = await branches.UpdateAsync(id, request, cancellationToken).ConfigureAwait(false);
        return ToResult(httpContext, result, localizer);
    }

    private static async Task<IResult> SetActiveAsync(
        Guid id,
        [FromBody] SetBranchActiveRequest request,
        HttpContext httpContext,
        IBranchSettingsService branches,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken)
    {
        var result = await branches.SetActiveAsync(id, request.IsActive, cancellationToken)
            .ConfigureAwait(false);
        return ToResult(httpContext, result, localizer);
    }

    private static async Task<IResult> DeleteAsync(
        Guid id,
        HttpContext httpContext,
        IBranchSettingsService branches,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken)
    {
        var result = await branches.DeleteAsync(id, cancellationToken).ConfigureAwait(false);
        if (result.IsSuccess)
        {
            return Results.NoContent();
        }

        return ToProblem(httpContext, result.ErrorCode, localizer);
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
            .FirstOrDefault(c => c is not null && c.StartsWith("branches.", StringComparison.Ordinal))
            ?? ErrorCodes.BranchesValidationFailed;
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
        var code = errorCode ?? ErrorCodes.BranchesValidationFailed;
        var status = code switch
        {
            ErrorCodes.BranchesNotFound or ErrorCodes.BranchesLegalEntityNotFound => StatusCodes.Status404NotFound,
            ErrorCodes.BranchesInUse => StatusCodes.Status409Conflict,
            ErrorCodes.BranchesLastActive => StatusCodes.Status400BadRequest,
            ErrorCodes.BranchesDuplicateName => StatusCodes.Status400BadRequest,
            _ => StatusCodes.Status400BadRequest,
        };
        return Results.Problem(ApiProblemDetails.Create(httpContext, status, code, localizer));
    }
}
