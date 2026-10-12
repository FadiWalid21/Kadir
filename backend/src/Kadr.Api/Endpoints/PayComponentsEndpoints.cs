using FluentValidation;
using Kadr.Api.Http;
using Kadr.Api.Localization;
using Kadr.Platform.Application.Abstractions;
using Kadr.Platform.Application.Payroll;
using Kadr.SharedKernel.Constants;
using Microsoft.AspNetCore.Mvc;

namespace Kadr.Api.Endpoints;

public static class PayComponentsEndpoints
{
    public static IEndpointRouteBuilder MapPayComponentsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/payroll/pay-components").WithTags("PayComponents");

        group.MapGet(string.Empty, ListAsync)
            .RequireAuthorization(PermissionCodes.SalaryRead)
            .WithName("ListPayComponents");

        group.MapGet("/{id:guid}", GetAsync)
            .RequireAuthorization(PermissionCodes.SalaryRead)
            .WithName("GetPayComponent");

        group.MapPost(string.Empty, CreateAsync)
            .RequireAuthorization(PermissionCodes.SalaryManage)
            .WithName("CreatePayComponent");

        group.MapPut("/{id:guid}", UpdateAsync)
            .RequireAuthorization(PermissionCodes.SalaryManage)
            .WithName("UpdatePayComponent");

        group.MapPost("/{id:guid}/deactivate", DeactivateAsync)
            .RequireAuthorization(PermissionCodes.SalaryManage)
            .WithName("DeactivatePayComponent");

        return app;
    }

    private static async Task<IResult> ListAsync(
        HttpContext httpContext,
        IPayComponentService payComponents,
        IErrorLocalizer localizer,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? q = null,
        [FromQuery] bool? isActive = null,
        CancellationToken cancellationToken = default)
    {
        var result = await payComponents.ListAsync(page, pageSize, q, isActive, cancellationToken)
            .ConfigureAwait(false);
        return ToResult(httpContext, result, localizer);
    }

    private static async Task<IResult> GetAsync(
        Guid id,
        HttpContext httpContext,
        IPayComponentService payComponents,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken)
    {
        var result = await payComponents.GetAsync(id, cancellationToken).ConfigureAwait(false);
        return ToResult(httpContext, result, localizer);
    }

    private static async Task<IResult> CreateAsync(
        [FromBody] CreatePayComponentRequest request,
        HttpContext httpContext,
        IPayComponentService payComponents,
        IValidator<CreatePayComponentRequest> validator,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken)
    {
        var invalid = await ValidateAsync(request, validator, httpContext, localizer, cancellationToken)
            .ConfigureAwait(false);
        if (invalid is not null)
        {
            return invalid;
        }

        var result = await payComponents.CreateAsync(request, cancellationToken).ConfigureAwait(false);
        return ToResult(httpContext, result, localizer);
    }

    private static async Task<IResult> UpdateAsync(
        Guid id,
        [FromBody] UpdatePayComponentRequest request,
        HttpContext httpContext,
        IPayComponentService payComponents,
        IValidator<UpdatePayComponentRequest> validator,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken)
    {
        var invalid = await ValidateAsync(request, validator, httpContext, localizer, cancellationToken)
            .ConfigureAwait(false);
        if (invalid is not null)
        {
            return invalid;
        }

        var result = await payComponents.UpdateAsync(id, request, cancellationToken).ConfigureAwait(false);
        return ToResult(httpContext, result, localizer);
    }

    private static async Task<IResult> DeactivateAsync(
        Guid id,
        HttpContext httpContext,
        IPayComponentService payComponents,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken)
    {
        var result = await payComponents.DeactivateAsync(id, cancellationToken).ConfigureAwait(false);
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
            .FirstOrDefault(c => c is not null && c.StartsWith("salary.", StringComparison.Ordinal))
            ?? ErrorCodes.SalaryValidationFailed;
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
        var code = errorCode ?? ErrorCodes.SalaryValidationFailed;
        var status = code switch
        {
            ErrorCodes.SalaryNotFound => StatusCodes.Status404NotFound,
            ErrorCodes.SalaryDuplicateKey => StatusCodes.Status400BadRequest,
            _ => StatusCodes.Status400BadRequest,
        };
        return Results.Problem(ApiProblemDetails.Create(httpContext, status, code, localizer));
    }
}
