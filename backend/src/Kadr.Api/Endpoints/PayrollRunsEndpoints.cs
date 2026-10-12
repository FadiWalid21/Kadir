using FluentValidation;
using Kadr.Api.Http;
using Kadr.Api.Localization;
using Kadr.Platform.Application.Abstractions;
using Kadr.Platform.Application.Payroll;
using Kadr.SharedKernel.Constants;
using Microsoft.AspNetCore.Mvc;

namespace Kadr.Api.Endpoints;

public static class PayrollRunsEndpoints
{
    public static IEndpointRouteBuilder MapPayrollRunsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/payroll/runs").WithTags("PayrollRuns");

        group.MapGet(string.Empty, ListAsync)
            .RequireAuthorization(PermissionCodes.PayrollRun)
            .WithName("ListPayrollRuns");

        group.MapPost(string.Empty, CreateAsync)
            .RequireAuthorization(PermissionCodes.PayrollRun)
            .WithName("CreatePayrollRun");

        group.MapGet("/{id:guid}", GetAsync)
            .RequireAuthorization(PermissionCodes.PayrollRun)
            .WithName("GetPayrollRun");

        group.MapPost("/{id:guid}/calculate", CalculateAsync)
            .RequireAuthorization(PermissionCodes.PayrollRun)
            .WithName("CalculatePayrollRun");

        group.MapPost("/{id:guid}/submit", SubmitAsync)
            .RequireAuthorization(PermissionCodes.PayrollRun)
            .WithName("SubmitPayrollRun");

        group.MapPost("/{id:guid}/approve", ApproveAsync)
            .RequireAuthorization(PermissionCodes.PayrollApprove)
            .WithName("ApprovePayrollRun");

        group.MapPost("/{id:guid}/lock", LockAsync)
            .RequireAuthorization(PermissionCodes.PayrollLock)
            .WithName("LockPayrollRun");

        group.MapPost("/{id:guid}/reopen", ReopenAsync)
            .RequireAuthorization(PermissionCodes.PayrollReopen)
            .WithName("ReopenPayrollRun");

        group.MapGet("/{id:guid}/entries", ListEntriesAsync)
            .RequireAuthorization(PermissionCodes.PayrollRun)
            .WithName("ListPayrollRunEntries");

        group.MapGet("/entries/{entryId:guid}", GetEntryAsync)
            .RequireAuthorization(PermissionCodes.PayrollRun)
            .WithName("GetPayrollEntry");

        group.MapPost("/entries/{entryId:guid}/hold", HoldEntryAsync)
            .RequireAuthorization(PermissionCodes.PayrollRun)
            .WithName("HoldPayrollEntry");

        return app;
    }

    private static async Task<IResult> ListAsync(
        HttpContext httpContext,
        IPayrollRunService runs,
        IErrorLocalizer localizer,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var result = await runs.ListAsync(page, pageSize, cancellationToken).ConfigureAwait(false);
        return ToResult(httpContext, result, localizer);
    }

    private static async Task<IResult> CreateAsync(
        [FromBody] CreatePayrollRunRequest request,
        HttpContext httpContext,
        IPayrollRunService runs,
        IValidator<CreatePayrollRunRequest> validator,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken)
    {
        var invalid = await ValidateAsync(request, validator, httpContext, localizer, cancellationToken)
            .ConfigureAwait(false);
        if (invalid is not null)
        {
            return invalid;
        }

        var result = await runs.CreateAsync(request, cancellationToken).ConfigureAwait(false);
        return ToResult(httpContext, result, localizer);
    }

    private static async Task<IResult> GetAsync(
        Guid id,
        HttpContext httpContext,
        IPayrollRunService runs,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken)
    {
        var result = await runs.GetAsync(id, cancellationToken).ConfigureAwait(false);
        return ToResult(httpContext, result, localizer);
    }

    private static async Task<IResult> CalculateAsync(
        Guid id,
        HttpContext httpContext,
        IPayrollRunService runs,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken)
    {
        var result = await runs.CalculateAsync(id, cancellationToken).ConfigureAwait(false);
        return ToResult(httpContext, result, localizer);
    }

    private static async Task<IResult> SubmitAsync(
        Guid id,
        HttpContext httpContext,
        IPayrollRunService runs,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken)
    {
        var result = await runs.SubmitAsync(id, cancellationToken).ConfigureAwait(false);
        return ToResult(httpContext, result, localizer);
    }

    private static async Task<IResult> ApproveAsync(
        Guid id,
        HttpContext httpContext,
        IPayrollRunService runs,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken)
    {
        var result = await runs.ApproveAsync(id, cancellationToken).ConfigureAwait(false);
        return ToResult(httpContext, result, localizer);
    }

    private static async Task<IResult> LockAsync(
        Guid id,
        HttpContext httpContext,
        IPayrollRunService runs,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken)
    {
        var result = await runs.LockAsync(id, cancellationToken).ConfigureAwait(false);
        return ToResult(httpContext, result, localizer);
    }

    private static async Task<IResult> ReopenAsync(
        Guid id,
        [FromBody] ReopenPayrollRunRequest request,
        HttpContext httpContext,
        IPayrollRunService runs,
        IValidator<ReopenPayrollRunRequest> validator,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken)
    {
        var invalid = await ValidateAsync(request, validator, httpContext, localizer, cancellationToken)
            .ConfigureAwait(false);
        if (invalid is not null)
        {
            return invalid;
        }

        var result = await runs.ReopenAsync(id, request, cancellationToken).ConfigureAwait(false);
        return ToResult(httpContext, result, localizer);
    }

    private static async Task<IResult> ListEntriesAsync(
        Guid id,
        HttpContext httpContext,
        IPayrollRunService runs,
        IErrorLocalizer localizer,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? q = null,
        [FromQuery] Guid? departmentId = null,
        [FromQuery] bool? warnings = null,
        [FromQuery] bool? held = null,
        CancellationToken cancellationToken = default)
    {
        var result = await runs
            .ListEntriesAsync(id, page, pageSize, q, departmentId, warnings, held, cancellationToken)
            .ConfigureAwait(false);
        return ToResult(httpContext, result, localizer);
    }

    private static async Task<IResult> GetEntryAsync(
        Guid entryId,
        HttpContext httpContext,
        IPayrollRunService runs,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken)
    {
        var result = await runs.GetEntryAsync(entryId, cancellationToken).ConfigureAwait(false);
        return ToResult(httpContext, result, localizer);
    }

    private static async Task<IResult> HoldEntryAsync(
        Guid entryId,
        [FromBody] HoldPayrollEntryRequest request,
        HttpContext httpContext,
        IPayrollRunService runs,
        IValidator<HoldPayrollEntryRequest> validator,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken)
    {
        var invalid = await ValidateAsync(request, validator, httpContext, localizer, cancellationToken)
            .ConfigureAwait(false);
        if (invalid is not null)
        {
            return invalid;
        }

        var result = await runs.HoldEntryAsync(entryId, request, cancellationToken).ConfigureAwait(false);
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

        var problem = ApiProblemDetails.Create(
            httpContext,
            StatusCodes.Status400BadRequest,
            ErrorCodes.PayrollRunValidationFailed,
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
        var code = errorCode ?? ErrorCodes.PayrollRunValidationFailed;
        var status = code switch
        {
            ErrorCodes.PayrollRunNotFound => StatusCodes.Status404NotFound,
            ErrorCodes.PayrollRunConflict => StatusCodes.Status409Conflict,
            ErrorCodes.PayrollRunImmutable => StatusCodes.Status400BadRequest,
            ErrorCodes.PayrollRunInvalidTransition => StatusCodes.Status400BadRequest,
            ErrorCodes.PayrollRunSecondApproverRequired => StatusCodes.Status400BadRequest,
            ErrorCodes.PayrollRulesNotConfirmed => StatusCodes.Status400BadRequest,
            _ => StatusCodes.Status400BadRequest,
        };
        return Results.Problem(ApiProblemDetails.Create(httpContext, status, code, localizer));
    }
}
