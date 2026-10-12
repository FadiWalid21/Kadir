using FluentValidation;
using Kadr.Api.Http;
using Kadr.Api.Localization;
using Kadr.Platform.Application.Abstractions;
using Kadr.Platform.Application.Payroll;
using Kadr.SharedKernel.Constants;
using Microsoft.AspNetCore.Mvc;

namespace Kadr.Api.Endpoints;

public static class PayrollRulesEndpoints
{
    public static IEndpointRouteBuilder MapPayrollRulesEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/payroll/rules").WithTags("PayrollRules");

        group.MapGet(string.Empty, ListAsync)
            .RequireAuthorization(PermissionCodes.PayrollRulesRead)
            .WithName("ListPayrollRules");

        group.MapGet("/{ruleKey}/history", HistoryAsync)
            .RequireAuthorization(PermissionCodes.PayrollRulesRead)
            .WithName("GetPayrollRuleHistory");

        group.MapPut("/{ruleKey}", UpsertAsync)
            .RequireAuthorization(PermissionCodes.PayrollRulesManage)
            .WithName("UpsertPayrollRule");

        group.MapPost("/{ruleKey}/reset", ResetAsync)
            .RequireAuthorization(PermissionCodes.PayrollRulesManage)
            .WithName("ResetPayrollRule");

        group.MapPost("/confirm", ConfirmAsync)
            .RequireAuthorization(PermissionCodes.PayrollRulesManage)
            .WithName("ConfirmPayrollRules");

        return app;
    }

    private static async Task<IResult> ListAsync(
        HttpContext httpContext,
        IPayrollRulesService rules,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken)
    {
        var result = await rules.ListCurrentAsync(cancellationToken).ConfigureAwait(false);
        return ToResult(httpContext, result, localizer);
    }

    private static async Task<IResult> HistoryAsync(
        string ruleKey,
        HttpContext httpContext,
        IPayrollRulesService rules,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken)
    {
        var result = await rules.GetHistoryAsync(ruleKey, cancellationToken).ConfigureAwait(false);
        return ToResult(httpContext, result, localizer);
    }

    private static async Task<IResult> UpsertAsync(
        string ruleKey,
        [FromBody] UpsertPayrollRuleRequest request,
        HttpContext httpContext,
        IPayrollRulesService rules,
        IValidator<UpsertPayrollRuleRequest> validator,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken)
    {
        var invalid = await ValidateAsync(request, validator, httpContext, localizer, cancellationToken)
            .ConfigureAwait(false);
        if (invalid is not null)
        {
            return invalid;
        }

        var result = await rules.UpsertAsync(ruleKey, request, cancellationToken).ConfigureAwait(false);
        return ToResult(httpContext, result, localizer);
    }

    private static async Task<IResult> ResetAsync(
        string ruleKey,
        [FromBody] UpsertPayrollRuleRequest request,
        HttpContext httpContext,
        IPayrollRulesService rules,
        IValidator<UpsertPayrollRuleRequest> validator,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken)
    {
        var invalid = await ValidateAsync(request, validator, httpContext, localizer, cancellationToken)
            .ConfigureAwait(false);
        if (invalid is not null)
        {
            return invalid;
        }

        var result = await rules
            .ResetAsync(ruleKey, request.Reason, request.EffectiveFrom, cancellationToken)
            .ConfigureAwait(false);
        return ToResult(httpContext, result, localizer);
    }

    private static async Task<IResult> ConfirmAsync(
        [FromBody] ConfirmPayrollRulesRequest request,
        HttpContext httpContext,
        IPayrollRulesService rules,
        IValidator<ConfirmPayrollRulesRequest> validator,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken)
    {
        var invalid = await ValidateAsync(request, validator, httpContext, localizer, cancellationToken)
            .ConfigureAwait(false);
        if (invalid is not null)
        {
            return invalid;
        }

        var result = await rules.ConfirmForLockAsync(request, cancellationToken).ConfigureAwait(false);
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
            ErrorCodes.PayrollRulesValidationFailed,
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

        var code = result.ErrorCode ?? ErrorCodes.PayrollRulesValidationFailed;
        var status = code switch
        {
            ErrorCodes.PayrollRulesNotFound => StatusCodes.Status404NotFound,
            ErrorCodes.PayrollRulesForbidden => StatusCodes.Status403Forbidden,
            _ => StatusCodes.Status400BadRequest,
        };
        return Results.Problem(ApiProblemDetails.Create(httpContext, status, code, localizer));
    }
}
